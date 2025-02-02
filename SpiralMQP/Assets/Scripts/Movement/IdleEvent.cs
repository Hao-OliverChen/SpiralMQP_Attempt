using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine;

[DisallowMultipleComponent]
public class IdleEvent : MonoBehaviour
{
    public event Action<IdleEvent> OnIdle;

    // Wrapper method 
    public void CallIdleEvent()
    {
        OnIdle?.Invoke(this);
    }
}
