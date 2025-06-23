using CrawfisSoftware;
using CrawfisSoftware.EventManagement;

using System;
using System.Collections;
using System.Threading.Tasks;

using Unity.InferenceEngine;
using Unity.Mathematics;

using UnityEngine;

public class PoseDetectionRefactored : MonoBehaviour
{
    public ModelAsset poseDetector;
    public ModelAsset poseLandmarker;
    public TextAsset anchorsCSV;
    public float scoreThreshold = 0.75f;

    const int k_NumAnchors = 2254;
    float[,] m_Anchors;

    const int k_NumKeypoints = 33;
    const int detectorInputSize = 224;
    const int landmarkerInputSize = 256;

    Worker m_PoseDetectorWorker;
    Worker m_PoseLandmarkerWorker;
    Tensor<float> m_DetectorInput;
    Tensor<float> m_LandmarkerInput;
    Awaitable m_DetectAwaitable;
    Model m_PoseDetectorModel;

    Texture _texture;
    bool _textureUpdated = false;
    float m_TextureWidth;
    float m_TextureHeight;
    float2x3 M = new();

    // Event Data
    private Vector3[] skeleton = new Vector3[k_NumKeypoints];
    private bool[] isTracked = new bool[k_NumKeypoints];
    private SkeletalData _skeletalData = new SkeletalData();
    private PersonBoundingCircle _personBoundingCircle = new PersonBoundingCircle();
    private FaceBoundingBox _faceBoundingBox = new FaceBoundingBox();
    private Coroutine _detectionCoroutine;

    // Helper to bridge async Task to IEnumerator for coroutines
    private class TaskYieldInstruction : CustomYieldInstruction
    {
        private readonly Task m_Task;
        public TaskYieldInstruction(Task task) { m_Task = task; }
        public override bool keepWaiting => !m_Task.IsCompleted;
    }

    public void Start()
    {
        EventsPublisherSimple.Instance.SubscribeToEvent("ImageUpdated", OnImageUpdated);
        EventsPublisherSimple.Instance.SubscribeToEvent("ImageSourceChanged", OnImageSourceChanged);
        m_PoseDetectorModel = CreatePersonDetectorGraph();
        CreateSkeletalTracker();
        CreatePersonDetectorWorker();
    }

    private void OnImageSourceChanged(object sender, object eventData)
    {
        if (_detectionCoroutine != null) StopCoroutine(_detectionCoroutine);
        _detectionCoroutine = null;
        _textureUpdated = true;
        Debug.Log("Image source has been changed.");
        _detectionCoroutine = StartCoroutine(StartDetecting());
    }

    private IEnumerator StartDetecting()
    {
        if (m_DetectAwaitable != null && !m_DetectAwaitable.IsCompleted)
            m_DetectAwaitable.Cancel(); // Cancel any ongoing detection
        m_DetectAwaitable = null;
        yield return null;
        yield return null;

        var detectionLoopTask = RunDetectionLoop();
        yield return new TaskYieldInstruction(detectionLoopTask);
    }

    private void OnImageUpdated(object sender, object textureObject)
    {
        Texture texture = textureObject as Texture;
        if (texture != null)
        {
            _texture = texture;
            _textureUpdated = true;
        }
    }

    private async Task RunDetectionLoop()
    {
        while (true)
        //while (!Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            if (!_textureUpdated)
            {
                await Task.Yield(); // Wait for the next frame if no texture update
                continue;
            }
            _textureUpdated = false;
            try
            {
                //EventsPublisherSimple.Instance.PublishEvent("ImageUpdated", this, texture);
                m_DetectAwaitable = Detect(_texture);
                await m_DetectAwaitable;
            }
            catch (OperationCanceledException)
            {
                break;
            }
            finally
            {
                m_DetectAwaitable = null; // Reset the awaitable after completion or cancellation
            }
        }

        OnClose();
    }

    private void CreateSkeletalTracker()
    {
        var poseLandmarkerModel = ModelLoader.Load(poseLandmarker);
        m_PoseLandmarkerWorker = new Worker(poseLandmarkerModel, BackendType.GPUCompute);

        m_DetectorInput = new Tensor<float>(new TensorShape(1, detectorInputSize, detectorInputSize, 3));
        m_LandmarkerInput = new Tensor<float>(new TensorShape(1, landmarkerInputSize, landmarkerInputSize, 3));
    }

    private void CreatePersonDetectorWorker()
    {
        m_PoseDetectorWorker = new Worker(m_PoseDetectorModel, BackendType.GPUCompute);
    }

    private Model CreatePersonDetectorGraph()
    {
        // Load anchor boxes (predefined bounding box templates) used by the pose detection model.
        // These anchors help the model efficiently search for possible human poses in the image.
        // Each anchor represents a candidate region for a person, not just a face or arbitrary object.
        m_Anchors = BlazeUtils.LoadAnchors(anchorsCSV.text, k_NumAnchors);
        // Load the pose detection model (used to find people in the image).
        Model m_PoseDetectorModel = ModelLoader.Load(poseDetector);
        // post process the model to filter scores + argmax select the best pose
        // Post-process the model to filter out low-confidence detections and select the best pose / person.
        // This step improves accuracy by only keeping the most likely person.
        var graph = new FunctionalGraph();
        var input = graph.AddInput(m_PoseDetectorModel, 0);
        // Run a dummy forward pass of the pose detection model using placeholder input.
        // This is not for actual detection, but to discover the output tensor shapes and set up the computation graph.
        // The values in 'boxes' and 'scores' are not meaningful at this stage.
        var outputs = Functional.Forward(m_PoseDetectorModel, input);
        var boxes = outputs[0]; // (1, 2254, 12) - predicted bounding boxes for all anchors (dummy data)
        var scores = outputs[1]; // (1, 2254, 1) - confidence scores for all anchors (dummy data)
        // Post-process the outputs to define which tensors to keep for real inference.
        // This sets up the graph to only output the best detection, optimizing later inference.
        var idx_scores_boxes = BlazeUtils.ArgMaxFiltering(boxes, scores);
        m_PoseDetectorModel = graph.Compile(idx_scores_boxes.Item1, idx_scores_boxes.Item2, idx_scores_boxes.Item3);
        return m_PoseDetectorModel;
    }

    Vector3 ImageToWorld(Vector2 position)
    {
        return (position - 0.5f * new Vector2(m_TextureWidth, m_TextureHeight)) / m_TextureHeight;
    }
    async Awaitable Detect(Texture texture)
    {
        //if (m_TextureWidth != texture.width || m_TextureHeight != texture.height)
        {
            m_TextureWidth = texture.width;
            m_TextureHeight = texture.height;
            var size = Mathf.Max(m_TextureWidth, m_TextureHeight);

            // The affine transformation matrix to go from tensor coordinates to image coordinates
            var scale = size / (float)detectorInputSize;
            M = BlazeUtils.mul(BlazeUtils.TranslationMatrix(0.5f * (new Vector2(m_TextureWidth, m_TextureHeight) + new Vector2(-size, size))), BlazeUtils.ScaleMatrix(new Vector2(scale, -scale)));
        }

        BlazeUtils.SampleImageAffine(texture, m_DetectorInput, M);

        m_PoseDetectorWorker.Schedule(m_DetectorInput);

        var outputIdxAwaitable = (m_PoseDetectorWorker.PeekOutput(0) as Tensor<int>).ReadbackAndCloneAsync();
        var outputScoreAwaitable = (m_PoseDetectorWorker.PeekOutput(1) as Tensor<float>).ReadbackAndCloneAsync();
        var outputBoxAwaitable = (m_PoseDetectorWorker.PeekOutput(2) as Tensor<float>).ReadbackAndCloneAsync();

        using var outputIdx = await outputIdxAwaitable;
        using var outputScore = await outputScoreAwaitable;
        using var outputBox = await outputBoxAwaitable;

        float score = outputScore[0];
        var scorePassesThreshold = score >= scoreThreshold;
        if (!scorePassesThreshold)
        {
            EventsPublisherSimple.Instance.PublishEvent("NoFaceDetected", this, null);
            EventsPublisherSimple.Instance.PublishEvent("NoPersonDetected", this, null);
            return;
        }
        //SetEnabledForBoundingBoxes(scorePassesThreshold);

        var idx = outputIdx[0];

        var anchorPosition = detectorInputSize * new float2(m_Anchors[idx, 0], m_Anchors[idx, 1]);

        // Extract positions of the resulting bounding box from the AI.
        float2 boundingBoxCenterOffset = new(outputBox[0, 0, 0], outputBox[0, 0, 1]);
        float2 topRightBoundingBoxOffset = new(outputBox[0, 0, 0] + 0.5f * outputBox[0, 0, 2], outputBox[0, 0, 1] + 0.5f * outputBox[0, 0, 3]);
        float2 leftHipOffset = new(outputBox[0, 0, 4 + 2 * 0 + 0], outputBox[0, 0, 4 + 2 * 0 + 1]);
        float2 rightHipOffset = new(outputBox[0, 0, 4 + 2 * 1 + 0], outputBox[0, 0, 4 + 2 * 1 + 1]);

        var faceCenterImageSpace = BlazeUtils.mul(M, anchorPosition + boundingBoxCenterOffset);
        var faceTopRightImageSpace = BlazeUtils.mul(M, anchorPosition + topRightBoundingBoxOffset);
        // Determine the rotation of the person based on the first two keyPoints:
        //    the left and right hips (but different than those determined with the landmarks :-()).
        var leftHipImageSpace = BlazeUtils.mul(M, anchorPosition + leftHipOffset);
        var rightHipImageSpace = BlazeUtils.mul(M, anchorPosition + rightHipOffset);
        var delta_ImageSpace = rightHipImageSpace - leftHipImageSpace;

        var dScale = 1.25f;
        var radius = dScale * math.length(delta_ImageSpace);
        var theta = math.atan2(delta_ImageSpace.y, delta_ImageSpace.x);
        var origin2 = new float2(0.5f * landmarkerInputSize, 0.5f * landmarkerInputSize); //(128,128)
        //origin2 = 0.5f * (leftShoulderImageSpace + rightShoulderImageSpace);
        var scale2 = radius / (0.5f * landmarkerInputSize);
        var M2 = BlazeUtils.mul(BlazeUtils.mul(BlazeUtils.mul(BlazeUtils.TranslationMatrix(leftHipImageSpace), BlazeUtils.ScaleMatrix(new float2(scale2, -scale2))), BlazeUtils.RotationMatrix(0.5f * Mathf.PI - theta)), BlazeUtils.TranslationMatrix(-origin2));

        var faceBoxSize = 2f * (faceTopRightImageSpace - faceCenterImageSpace);

        Vector3 faceWorldPosition = ImageToWorld(faceCenterImageSpace);
        float2 boundingBoxHeight = faceBoxSize / m_TextureHeight;
        Vector3 leftHipPosition = ImageToWorld(leftHipImageSpace);
        float personDetectionRadius = radius / m_TextureHeight;

        _personBoundingCircle.origin = leftHipPosition;
        _personBoundingCircle.radius = 0.8f * personDetectionRadius;
        _faceBoundingBox.faceWorldPosition = faceWorldPosition;
        _faceBoundingBox.boundingBoxHeight = boundingBoxHeight;
        EventsPublisherSimple.Instance.PublishEvent("PersonDetected", this, _personBoundingCircle);
        EventsPublisherSimple.Instance.PublishEvent("FaceDetected", this, _faceBoundingBox);
        //ShowBoundingBoxes(faceWorldPosition, boundingBoxHeight, leftHipPosition, personDetectionRadius);

        BlazeUtils.SampleImageAffine(texture, m_LandmarkerInput, M2);
        m_PoseLandmarkerWorker.Schedule(m_LandmarkerInput);

        var landmarksAwaitable = (m_PoseLandmarkerWorker.PeekOutput("Identity") as Tensor<float>).ReadbackAndCloneAsync();
        using var landmarks = await landmarksAwaitable; // (1,195)
        ShowTrackedKeyPoints(M2, landmarks);
    }

    private void ShowTrackedKeyPoints(float2x3 M2, Tensor<float> landmarks)
    {
        for (var i = 0; i < k_NumKeypoints; i++)
        {
            // https://arxiv.org/pdf/2006.10204
            var position_ImageSpace = BlazeUtils.mul(M2, new float2(landmarks[5 * i + 0], landmarks[5 * i + 1]));
            var visibility = landmarks[5 * i + 3];
            var presence = landmarks[5 * i + 4];

            // z-position is in unit cube centered on hips
            Vector3 position_WorldSpace = ImageToWorld(position_ImageSpace) + new Vector3(0, 0, landmarks[5 * i + 2] / m_TextureHeight);
            skeleton[i] = position_WorldSpace;
            isTracked[i] = visibility > 0.5f && presence > 0.5f;
        }
        _skeletalData.isTracked = isTracked;
        _skeletalData.skeletalPositions = skeleton;
        EventsPublisherSimple.Instance.PublishEvent("Skeleton", this, _skeletalData);
    }

    void OnClose()
    {
        // Cancel and await any running detection
        if (m_DetectAwaitable != null && !m_DetectAwaitable.IsCompleted)
        {
            // Should never really get here unless OnDestroy is called while detection is running.
            try
            {
                m_DetectAwaitable.Cancel();
                // Optionally, you could await here if OnClose was async, but in Unity MonoBehaviour it's not.
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Exception during Awaitable.Cancel(): {ex.Message}");
            }
        }
        m_DetectAwaitable = null;
    }

    private void OnDestroy()
    {
        OnClose();
        m_PoseDetectorWorker?.Dispose(); m_PoseDetectorWorker = null;
        m_PoseLandmarkerWorker?.Dispose(); m_PoseLandmarkerWorker = null;
        m_DetectorInput?.Dispose(); m_DetectorInput = null;
        m_LandmarkerInput?.Dispose(); m_LandmarkerInput = null;
    }
}
