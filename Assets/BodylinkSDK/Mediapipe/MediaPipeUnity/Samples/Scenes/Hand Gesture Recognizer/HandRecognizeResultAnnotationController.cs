// Copyright (c) 2023 homuler
//
// Use of this source code is governed by an MIT-style
// license that can be found in the LICENSE file or at
// https://opensource.org/licenses/MIT.

using UnityEngine;

using Mediapipe.Tasks.Vision.GestureRecognizer;

namespace Mediapipe.Unity
{
  public class HandRecognizeResultAnnotationController : AnnotationController<MultiHandLandmarkListAnnotation>
  {
    [SerializeField] private bool _visualizeZ = false;

    private readonly object _currentTargetLock = new object();
    private GestureRecognizerResult _currentTarget;

    public void DrawNow(GestureRecognizerResult target)
    {
      target.CloneTo(ref _currentTarget);
      SyncNow();
    }

    public void DrawLater(GestureRecognizerResult target) => UpdateCurrentTarget(target);

    protected void UpdateCurrentTarget(GestureRecognizerResult newTarget)
    {
      lock (_currentTargetLock)
      {
        newTarget.CloneTo(ref _currentTarget);
        isStale = true;
      }
    }

    protected override void SyncNow()
    {
      lock (_currentTargetLock)
      {
        isStale = false;
        annotation.SetHandedness(_currentTarget.handedness);
        annotation.Draw(_currentTarget.handLandmarks, _visualizeZ);
      }
    }
  }
}
