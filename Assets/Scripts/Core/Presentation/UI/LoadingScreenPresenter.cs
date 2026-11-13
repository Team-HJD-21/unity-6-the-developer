using System;
using TeamHJD.Game.Contracts;
using UnityEngine;
using UnityEngine.EventSystems;

namespace TeamHJD.Game.Presentation
{
    public sealed class LoadingScreenPresenter : IScreenPresenter
    {
        private readonly AppLoadingView _view;
        private readonly Func<float> _progress;
        private readonly Action _released;
        private bool _disposed;
        public LoadingScreenPresenter(Transform parent, Func<float> progress, Action released, Font font = null, AppLoadingView authored = null)
        {
            _progress = progress ?? throw new ArgumentNullException(nameof(progress));
            _released = released ?? throw new ArgumentNullException(nameof(released));
            if (authored == null) authored = Resources.Load<AppScreenStyle>("Core/AppScreenStyle")?.LoadingPrefab;
            if (authored == null) throw new InvalidOperationException("Assign the authored loading view.");
            _view = UnityEngine.Object.Instantiate(authored, parent, false); _view.name = "Screen:Loading";
            Refresh();
        }
        public void Refresh() { if (!_disposed) _view.ShowProgress(_progress()); }
        public void SetFocused(bool focused)
        {
            if (_disposed) return;
            _view.gameObject.SetActive(focused);
            if (focused && EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        }
        public void Dispose()
        {
            if (_disposed) return; _disposed = true;
            _released(); UnityEngine.Object.Destroy(_view.gameObject);
        }
    }
}
