namespace QuickPad.Input;

/// <summary>Detects a quick double tap of Ctrl (either side) from a stream of key events.</summary>
public sealed class DoubleTapDetector(int windowMs = 350, int maxHoldMs = 300)
{
    long _firstUp = -1;     // when the first tap was released, -1 if none pending
    long _downAt;
    bool _ctrlDown, _dirty, _secondTap;

    static bool IsCtrl(int vk) => vk is 0x11 or 0xA2 or 0xA3;

    public bool OnKey(int vk, bool isDown, long timestampMs)
    {
        if (!IsCtrl(vk))
        {
            if (isDown) { _firstUp = -1; _dirty = true; }
            return false;
        }

        if (isDown)
        {
            if (_ctrlDown) return false; // auto-repeat
            _ctrlDown = true;
            _dirty = false;
            _downAt = timestampMs;
            _secondTap = _firstUp >= 0 && timestampMs - _firstUp <= windowMs;
            return false;
        }

        if (!_ctrlDown) return false;
        _ctrlDown = false;
        if (_dirty || timestampMs - _downAt > maxHoldMs) { Reset(); return false; }
        if (_secondTap) { Reset(); return true; }
        _firstUp = timestampMs;
        return false;
    }

    public void Reset() { _firstUp = -1; _ctrlDown = _dirty = _secondTap = false; }
}
