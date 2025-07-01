using System;
using System.Globalization;
using Unity.Mathematics;
using Unity.InferenceEngine;
using UnityEngine;

public static class BlazeUtils
{
    // matrix utility
    public static float2x3 mul(float2x3 a, float2x3 b)
    {
        return new float2x3(
            a[0][0] * b[0][0] + a[1][0] * b[0][1],
            a[0][0] * b[1][0] + a[1][0] * b[1][1],
            a[0][0] * b[2][0] + a[1][0] * b[2][1] + a[2][0],
            a[0][1] * b[0][0] + a[1][1] * b[0][1],
            a[0][1] * b[1][0] + a[1][1] * b[1][1],
            a[0][1] * b[2][0] + a[1][1] * b[2][1] + a[2][1]
        );
    }

    public static float2 mul(float2x3 a, float2 b)
    {
        return new float2(
            a[0][0] * b.x + a[1][0] * b.y + a[2][0],
            a[0][1] * b.x + a[1][1] * b.y + a[2][1]
        );
    }

    public static float2x3 RotationMatrix(float theta)
    {
        var sinTheta = math.sin(theta);
        var cosTheta = math.cos(theta);
        return new float2x3(
            cosTheta, -sinTheta, 0,
            sinTheta, cosTheta, 0
        );
    }

    public static float2x3 TranslationMatrix(float2 delta)
    {
        return new float2x3(
            1, 0, delta.x,
            0, 1, delta.y
        );
    }

    public static float2x3 ScaleMatrix(float2 scale)
    {
        return new float2x3(
            scale.x, 0, 0,
            0, scale.y, 0
        );
    }

    /// <summary>
    /// Returns an affine matrix that fits the entire source texture into the destination tensor,
    /// preserving aspect ratio and adding letterboxing (black bars) if needed.
    /// The whole image will be visible, possibly with padding.
    /// </summary>
    /// <param name="srcWidth">Source texture width.</param>
    /// <param name="srcHeight">Source texture height.</param>
    /// <param name="dstSize">Destination tensor size (e.g., 224 or 256).</param>
    /// <returns>Affine matrix mapping tensor coordinates to texture coordinates.</returns>
    public static float2x3 FitTextureToTensorMatrix(float srcWidth, float srcHeight, float dstSize)
    {
        // Scale so the largest side fits the tensor, preserving aspect ratio (letterbox)
        float scale = Math.Max(srcWidth, srcHeight) / dstSize;
        float2 scaleVec = new float2(scale, scale);

        // Offset to center the image in the tensor
        float2 offset = 0.5f * (new float2(srcWidth, srcHeight) - new float2(dstSize, dstSize) * scale);

        // Compose: scale then translate
        return mul(
            TranslationMatrix(offset),
            ScaleMatrix(scaleVec)
        );
    }

    /// <summary>
    /// Returns an affine matrix that fills the destination tensor with the source texture,
    /// preserving aspect ratio but cropping as needed (center crop).
    /// The tensor will be fully filled, but some image content may be lost.
    /// </summary>
    /// <param name="srcWidth">Source texture width.</param>
    /// <param name="srcHeight">Source texture height.</param>
    /// <param name="dstSize">Destination tensor size (e.g., 224 or 256).</param>
    /// <returns>Affine matrix mapping tensor coordinates to texture coordinates.</returns>
    public static float2x3 FillTextureToTensorMatrix(float srcWidth, float srcHeight, float dstSize)
    {
        // Scale so the smallest side fills the tensor, preserving aspect ratio (center crop)
        float scale = Math.Min(srcWidth, srcHeight) / dstSize;
        float2 scaleVec = new float2(scale, scale);

        // Offset to center the crop
        float2 offset = 0.5f * (new float2(srcWidth, srcHeight) - new float2(dstSize, dstSize) * scale);

        // Compose: scale then translate
        return mul(
            TranslationMatrix(offset),
            ScaleMatrix(scaleVec)
        );
    }

    // model filtering utility
    static FunctionalTensor ScoreFiltering(FunctionalTensor rawScores, float scoreThreshold)
    {
        return Functional.Sigmoid(Functional.Clamp(rawScores, -scoreThreshold, scoreThreshold));
    }

    public static (FunctionalTensor, FunctionalTensor, FunctionalTensor) ArgMaxFiltering(FunctionalTensor rawBoxes, FunctionalTensor rawScores)
    {
        // Clamp all scores to be from -100 to 100. Not sure why this is needed.
        var detectionScores = ScoreFiltering(rawScores, 100f); // (1, 2254, 1)
        // Determine the best bounding box index to use according to the scores.
        var bestScoreIndex = Functional.ArgMax(rawScores, 1).Squeeze();
        // Select the bounding box with the highest score from all predictions.
        // The result is a tensor of shape (1, 1, 16):
        //   - 1 batch,
        //   - 1 bounding box (the best one),
        //   - 16 values describing the box (coordinates and possibly keypoints).
        var selectedBoxes = Functional.IndexSelect(rawBoxes, 1, bestScoreIndex).Unsqueeze(0); // (1, 1, 16)
        var selectedScores = Functional.IndexSelect(detectionScores, 1, bestScoreIndex).Unsqueeze(0); // (1, 1, 1)

        return (bestScoreIndex, selectedScores, selectedBoxes);
    }

    // image transform utility
    static ComputeShader s_ImageTransformShader = Resources.Load<ComputeShader>("ComputeShaders/ImageTransform");
    static int s_ImageSample = s_ImageTransformShader.FindKernel("ImageSample");
    static int s_Optr = Shader.PropertyToID("Optr");
    static int s_X_tex2D = Shader.PropertyToID("X_tex2D");
    static int s_O_height = Shader.PropertyToID("O_height");
    static int s_O_width = Shader.PropertyToID("O_width");
    static int s_O_channels = Shader.PropertyToID("O_channels");
    static int s_X_height = Shader.PropertyToID("X_height");
    static int s_X_width = Shader.PropertyToID("X_width");
    static int s_affineMatrix = Shader.PropertyToID("affineMatrix");

    static int IDivC(int v, int div)
    {
        return (v + div - 1) / div;
    }

    public static void SampleImageAffine(Texture srcTexture, Tensor<float> dstTensor, float2x3 M)
    {
        var tensorData = ComputeTensorData.Pin(dstTensor, false);

        s_ImageTransformShader.SetTexture(s_ImageSample, s_X_tex2D, srcTexture);
        s_ImageTransformShader.SetBuffer(s_ImageSample, s_Optr, tensorData.buffer);

        s_ImageTransformShader.SetInt(s_O_height, dstTensor.shape[1]);
        s_ImageTransformShader.SetInt(s_O_width, dstTensor.shape[2]);
        s_ImageTransformShader.SetInt(s_O_channels, dstTensor.shape[3]);
        s_ImageTransformShader.SetInt(s_X_height, srcTexture.height);
        s_ImageTransformShader.SetInt(s_X_width, srcTexture.width);

        s_ImageTransformShader.SetMatrix(s_affineMatrix, new Matrix4x4(new Vector4(M[0][0], M[0][1]), new Vector4(M[1][0], M[1][1]), new Vector4(M[2][0], M[2][1]), Vector4.zero));

        // Dispatch the compute shader kernel to process the output image in parallel, 
        // dividing the work into thread groups sized to cover the image height (rounded up to the nearest multiple of 8).
        s_ImageTransformShader.Dispatch(s_ImageSample, IDivC(dstTensor.shape[1], 8), IDivC(dstTensor.shape[1], 8), 1);
    }

    /// <summary>
    /// CPU implementation of the ImageTransform compute shader. Applies an affine transform to sample the source texture
    /// into the destination tensor, with sRGB conversion and out-of-bounds masking. Slower than GPU version.
    /// </summary>
    /// <param name="srcTexture">Source Texture2D (must be readable).</param>
    /// <param name="dstTensor">Destination tensor (NHWC: [1, height, width, 3]).</param>
    /// <param name="M">Affine matrix mapping output to input coordinates.</param>
    public static void SampleImageAffineCPU(Texture2D srcTexture, Tensor<float> dstTensor, float2x3 M)
    {
        int outHeight = dstTensor.shape[1];
        int outWidth = dstTensor.shape[2];
        int outChannels = dstTensor.shape[3];

        int srcWidth = srcTexture.width;
        int srcHeight = srcTexture.height;

        for (int oy = 0; oy < outHeight; oy++)
        {
            for (int ox = 0; ox < outWidth; ox++)
            {
                // Apply affine transform to get source coordinates
                float2 srcPos = mul(M, new float2(ox, oy));
                float u = srcPos.x / srcWidth;
                float v = srcPos.y / srcHeight;

                // Check if within bounds
                bool mask = u >= 0 && u <= 1 && v >= 0 && v <= 1;
                Color c = mask ? srcTexture.GetPixelBilinear(u, v) : Color.black;

                // sRGB conversion (matches shader)
                Vector3 rgb = new Vector3(c.r, c.g, c.b);
                Vector3 maskRGB = new Vector3(
                    rgb.x > 0.0031308f ? 1 : 0,
                    rgb.y > 0.0031308f ? 1 : 0,
                    rgb.z > 0.0031308f ? 1 : 0
                );
                Vector3 linearRGB = 12.92f * rgb;
                Vector3 srgbRGB = 1.055f * new Vector3(
                    Mathf.Pow(Mathf.Abs(rgb.x), 0.41666666666f),
                    Mathf.Pow(Mathf.Abs(rgb.y), 0.41666666666f),
                    Mathf.Pow(Mathf.Abs(rgb.z), 0.41666666666f)
                ) - new Vector3(0.055f, 0.055f, 0.055f);
                Vector3 outRGB = new Vector3(
                    maskRGB.x > 0 ? srgbRGB.x : linearRGB.x,
                    maskRGB.y > 0 ? srgbRGB.y : linearRGB.y,
                    maskRGB.z > 0 ? srgbRGB.z : linearRGB.z
                );

                // Write to tensor (NHWC)
                dstTensor[0, oy, ox, 0] = outRGB.x;
                dstTensor[0, oy, ox, 1] = outRGB.y;
                dstTensor[0, oy, ox, 2] = outRGB.z;
            }
        }
    }

    public static float[,] LoadAnchors(string csv, int numAnchors)
    {
        var anchors = new float[numAnchors, 4];
        var anchorLines = csv.Split('\n');

        for (var i = 0; i < numAnchors; i++)
        {
            var anchorValues = anchorLines[i].Split(',');
            for (var j = 0; j < 4; j++)
            {
                anchors[i, j] = float.Parse(anchorValues[j], CultureInfo.InvariantCulture);
            }
        }

        return anchors;
    }

    public static Texture2D ToTexture2D(Texture texture)
    {
        if (texture is Texture2D tex2D)
            return tex2D;

        // Assume texture is a RenderTexture
        RenderTexture currentRT = RenderTexture.active;
        RenderTexture rt = texture as RenderTexture;
        if (rt == null)
            throw new ArgumentException("Texture must be Texture2D or RenderTexture");

        Texture2D tex = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false);
        RenderTexture.active = rt;
        tex.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0);
        tex.Apply();
        RenderTexture.active = currentRT;
        return tex;
    }
}
