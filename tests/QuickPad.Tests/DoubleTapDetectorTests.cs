using QuickPad.Input;

namespace QuickPad.Tests;

public class DoubleTapDetectorTests
{
    const int Ctrl = 0xA2, C = 0x43;
    static bool Tap(DoubleTapDetector d, long t) { d.OnKey(Ctrl, true, t); return d.OnKey(Ctrl, false, t + 50); }

    [Fact] public void TwoTapsWithinWindow_Fires() { var d = new DoubleTapDetector(); Assert.False(Tap(d, 0)); Assert.True(Tap(d, 200)); }
    [Fact] public void TwoTapsTooFarApart_DoesNotFire() { var d = new DoubleTapDetector(); Tap(d, 0); Assert.False(Tap(d, 50 + 351 + 1)); }
    [Fact] public void OtherKeyBetween_DoesNotFire() { var d = new DoubleTapDetector(); Tap(d, 0); d.OnKey(C, true, 100); d.OnKey(C, false, 120); Assert.False(Tap(d, 150)); }
    [Fact] public void CtrlPlusC_ThenTap_DoesNotFire() { var d = new DoubleTapDetector(); d.OnKey(Ctrl, true, 0); d.OnKey(C, true, 30); d.OnKey(C, false, 60); d.OnKey(Ctrl, false, 90); Assert.False(Tap(d, 150)); }
    [Fact] public void HeldCtrl_DoesNotCountAsTap() { var d = new DoubleTapDetector(); d.OnKey(Ctrl, true, 0); for (var t = 30; t < 600; t += 30) d.OnKey(Ctrl, true, t); d.OnKey(Ctrl, false, 600); Assert.False(Tap(d, 700)); }
    [Fact] public void ThreeTaps_FireOnce() { var d = new DoubleTapDetector(); var fires = new[] { Tap(d, 0), Tap(d, 150), Tap(d, 300) }.Count(x => x); Assert.Equal(1, fires); }
}
