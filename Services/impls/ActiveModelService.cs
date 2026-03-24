using System;

namespace OB.Services.impls
{
    public class ActiveModelService : IActiveModelService
    {
        private string? _activeModelPath;

        public string? ActiveModelPath => _activeModelPath;

        public event EventHandler? ActiveModelChanged;

        public void SetActiveModel(string modelPath)
        {
            if (_activeModelPath == modelPath) return;
            _activeModelPath = modelPath;
            ActiveModelChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}