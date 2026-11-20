using UnityEngine;

namespace Grace.VWaterOriginal
{
    [CreateAssetMenu(menuName="Grace/VWater/Height Response Profile",fileName="DownwashHeightResponse")]
    public sealed class DownwashResponseProfile : ScriptableObject
    {
        [Tooltip("X = actual fan height above water in metres. Y = mist amount, before thrust/gain.")]
        public AnimationCurve mistAmount=Point(3.38f);
        [Tooltip("X = fan height (m). Y = spray-height setting (m), before thrust/gain.")]
        public AnimationCurve sprayHeight=Point(.20f);
        [Tooltip("X = fan height (m). Y = wave-height setting (m), before thrust/gain.")]
        public AnimationCurve waveHeight=Point(.11f);
        [Tooltip("X = fan height (m). Y = direct pressure-input magnitude, before thrust/gain.")]
        public AnimationCurve pressure=Point(.2309703f);
        [Tooltip("X = fan height (m). Y = direct surface-foam input, before thrust/gain.")]
        public AnimationCurve foam=Point(.4234455f);
        [Tooltip("X = fan height (m). Y = mist noise strength, 0 to 1. Independent of emission amount and launch height.")]
        public AnimationCurve mistTurbulence=Point(.35f);
        [Tooltip("Multiply airborne opacity by the angle curve. This does not change emission count or surface foam.")]
        public bool useMistViewFade=true;
        [Tooltip("X = angle from water plane in degrees (0 horizontal, 90 vertical). Y = airborne opacity multiplier, 0 to 1.")]
        public AnimationCurve mistOpacityByViewAngle=DefaultViewOpacity();

        public DownwashResponseValues Evaluate(float height)
        {
            return new DownwashResponseValues(Value(mistAmount,height),Value(sprayHeight,height),Value(waveHeight,height),Value(pressure,height),Value(foam,height),Value(mistTurbulence,height));
        }
        public static AnimationCurve DefaultViewOpacity()
        {
            return new AnimationCurve(new Keyframe(0,1),new Keyframe(10,1),new Keyframe(25,.10f),new Keyframe(45,.02f),new Keyframe(90,0))
                {preWrapMode=WrapMode.ClampForever,postWrapMode=WrapMode.ClampForever};
        }
        private static float Value(AnimationCurve curve,float height)
        { return curve!=null && curve.length>0 && !float.IsNaN(height) && !float.IsInfinity(height)?curve.Evaluate(height):0; }
        private static AnimationCurve Point(float value)
        { return new AnimationCurve(new Keyframe(1.9f,value)){preWrapMode=WrapMode.ClampForever,postWrapMode=WrapMode.ClampForever}; }
    }
}
