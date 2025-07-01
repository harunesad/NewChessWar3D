using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Clickables.Tracking
{
	public class MouseTracker : ICursorTracker
	{
		public override Vector2 GetScreenPos()
		{
#if ENABLE_INPUT_SYSTEM
			return Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
#elif ENABLE_LEGACY_INPUT_MANAGER
			return new Vector2(Input.mousePosition.x, Input.mousePosition.y);
#endif
		}
	}
}