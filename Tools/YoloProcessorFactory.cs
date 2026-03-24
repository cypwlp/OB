using Microsoft.ML.OnnxRuntime;
using OB.Services;
using OB.Services.impls;


namespace OB.Tools
{
    public static class YoloProcessorFactory
    {
        public static (IPreprocessor preprocessor, IPostprocessor postprocessor) Create(InferenceSession session,
                                                                                        float confThreshold = 0.25f,
                                                                                        float iouThreshold = 0.45f,
                                                                                        string[] classNames = null,
                                                                                        int originalWidth = 640,
                                                                                        int originalHeight = 640)
        {
            var preprocessor = YoloPreprocessor.FromSession(session);
            var postprocessor = new YoloPostprocessor(confThreshold, iouThreshold, classNames,
                                                      preprocessor._targetWidth, preprocessor._targetHeight,
                                                      originalWidth, originalHeight);
            return (preprocessor, postprocessor);
        }
    }
}
