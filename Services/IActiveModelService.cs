using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OB.Services
{
    public interface IActiveModelService
    {
        string? ActiveModelPath { get; }
        event EventHandler? ActiveModelChanged;
        void SetActiveModel(string modelPath);
    }
    
}
