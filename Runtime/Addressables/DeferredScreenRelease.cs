using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace SimpleUIScreensSystem.AddressableUI
{
    // Resource ownership must survive disabled roots and cancellation of gameplay coroutines.
    internal sealed class DeferredScreenRelease : IUpdateReceiver
    {
        private readonly List<AsyncOperationHandle<GameObject>> _handles;
        private readonly ResourceManager _owner;
        private readonly int _destroyFrame;

        internal DeferredScreenRelease(List<AsyncOperationHandle<GameObject>> handles)
        {
            _handles = handles;
            _owner = Addressables.ResourceManager;
            _destroyFrame = Time.frameCount;
            _owner.AddUpdateReceiver(this);
        }

        public void Update(float unscaledDeltaTime)
        {
            if (Time.frameCount <= _destroyFrame) return;
            _owner.RemoveUpdateReciever(this);
            foreach (var handle in _handles)
                if (handle.IsValid()) Addressables.Release(handle);
            _handles.Clear();
        }
    }
}
