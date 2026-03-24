using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using System.Collections.Generic;
using System.Linq;

namespace OB.Models
{
    // 用于描述单个输入或输出的信息
    public class OnnxTensorInfo
    {
        public string Name { get; set; }               // 张量名称
        public TensorElementType DataType { get; set; } // 元素类型
        public IReadOnlyList<int> Dimensions { get; set; } // 形状（可能包含 -1）
        public bool HasDynamicDimension => Dimensions?.Any(d => d == -1) ?? false;
    }

    public class OnnxModelInfo
    {
        // 模型基础信息
        public string ModelPath { get; set; }
        public string ModelName { get; set; }
        public string ProducerName { get; set; }
        public string GraphName { get; set; }
        public string Description { get; set; }
        public long Version { get; set; }

        // 输入/输出列表（支持多个）
        public List<OnnxTensorInfo> Inputs { get; set; } = new List<OnnxTensorInfo>();
        public List<OnnxTensorInfo> Outputs { get; set; } = new List<OnnxTensorInfo>();

        // 自定义元数据（字典形式，方便按需获取）
        public Dictionary<string, string> CustomMetadata { get; set; } = new Dictionary<string, string>();

        // 为了方便原有代码，可以保留单输入/单输出的快捷属性
        public string InputTensorName => Inputs.FirstOrDefault()?.Name;
        public string OutputTensorName => Outputs.FirstOrDefault()?.Name;
        public int[] InputDimensions => Inputs.FirstOrDefault()?.Dimensions?.ToArray();
        public int[] OutputDimensions => Outputs.FirstOrDefault()?.Dimensions?.ToArray();
    }
}