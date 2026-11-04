using System;
using UnityEditor;
using UnityEngine;

namespace Grace.VWaterOriginal.Editor
{
    internal sealed class DownwashCurvePanel
    {
        private static readonly string[] Names={"mistAmount","sprayHeight","waveHeight","pressure","foam","mistTurbulence"};
        private static readonly string[] Labels={"Mist amount","Spray height (m)","Wave height (m)","Pressure input magnitude","Surface foam input","Mist turbulence"};
        private static readonly float[] Maxima={8,3,.5f,1,1,1};
        private readonly DownwashCurveDraft draft=new DownwashCurveDraft();
        private float previewAngle=25;

        public void Draw(DownwashResponseProfile profile,float height)
        {
            var data=new SerializedObject(profile);data.Update();
            bool refresh=false;
            EditorGUILayout.HelpBox("X = fan height above water in metres. Y = effect setting before thrust / overall gain. One initial key keeps a constant value until you add more keys.",MessageType.Info);
            for(int i=0;i<Names.Length;i++)
            {
                var property=data.FindProperty(Names[i]);
                EditorGUI.BeginChangeCheck();
                var curve=EditorGUILayout.CurveField(new GUIContent(Labels[i]),property.animationCurveValue,
                    Color.HSVToRGB(i*.16f,.55f,1),new Rect(0,0,8,Maxima[i]));
                if(EditorGUI.EndChangeCheck()){property.animationCurveValue=curve;refresh=true;}
            }
            data.ApplyModifiedProperties();
            var value=profile.Evaluate(height);
            draft.Sync(profile.GetInstanceID(),height,value,refresh);
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Key values at "+height.ToString("F2")+" m",EditorStyles.boldLabel);
            for(int i=0;i<Names.Length;i++)draft.Values[i]=EditorGUILayout.FloatField(Labels[i],(float)draft.Values[i]);
            bool valid=!float.IsNaN(height) && !float.IsInfinity(height) && height>0;
            using(new EditorGUI.DisabledScope(!valid))
            {
                if(GUILayout.Button("Add / update keys at this height"))
                {
                    data.Update();
                    for(int i=0;i<Names.Length;i++)
                    {
                        var property=data.FindProperty(Names[i]);var curve=property.animationCurveValue??new AnimationCurve();
                        float output=(float)draft.Values[i];if(float.IsNaN(output)||float.IsInfinity(output))output=0;
                        output=Mathf.Clamp(output,0,Maxima[i]);
                        int found=-1;for(int k=0;k<curve.length;k++)if(Mathf.Abs(curve[k].time-height)<.0001f){found=k;break;}
                        if(found>=0){var key=curve[found];key.value=output;curve.MoveKey(found,key);}
                        else
                        {
                            int index=curve.AddKey(new Keyframe(height,output));
                            if(index>=0)
                            {
                                AnimationUtility.SetKeyLeftTangentMode(curve,index,AnimationUtility.TangentMode.ClampedAuto);
                                AnimationUtility.SetKeyRightTangentMode(curve,index,AnimationUtility.TangentMode.ClampedAuto);
                            }
                        }
                        property.animationCurveValue=curve;
                    }
                    data.ApplyModifiedProperties();draft.Invalidate();
                }
            }
            EditorGUILayout.HelpBox($"Evaluated profile at {height:F2} m:\nMist {value.MistAmount:F2}   Spray {value.SprayHeight:F2} m   Wave {value.WaveHeight:F2} m\nPressure {value.Pressure:F3}   Surface foam {value.Foam:F3}   Turbulence {value.MistTurbulence:F2}",MessageType.None);
            EditorGUILayout.Space();EditorGUILayout.LabelField("Airborne visibility by viewing angle",EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("0 degrees = side-on along the water. 90 degrees = straight down/up. Y multiplies particle opacity; it does not change surface foam or particle count.",MessageType.Info);
            data.Update();
            EditorGUILayout.PropertyField(data.FindProperty("useMistViewFade"),new GUIContent("Use view-angle fade"));
            var angleProperty=data.FindProperty("mistOpacityByViewAngle");
            EditorGUI.BeginChangeCheck();
            var angleCurve=EditorGUILayout.CurveField("Angle to opacity",angleProperty.animationCurveValue,Color.cyan,new Rect(0,0,90,1));
            if(EditorGUI.EndChangeCheck())angleProperty.animationCurveValue=angleCurve;
            data.ApplyModifiedProperties();
            previewAngle=EditorGUILayout.Slider("Preview view angle",previewAngle,0,90);
            float viewOpacity=profile.useMistViewFade?(profile.mistOpacityByViewAngle!=null && profile.mistOpacityByViewAngle.length>0?profile.mistOpacityByViewAngle.Evaluate(previewAngle):0):1;
            EditorGUILayout.LabelField("Airborne opacity multiplier",Mathf.Clamp01(viewOpacity).ToString("P0"));
            if(GUILayout.Button("Save profile (keeps edits after Play)"))AssetDatabase.SaveAssetIfDirty(profile);
        }
    }

    [CustomEditor(typeof(DownwashResponseProfile))]
    public sealed class DownwashResponseProfileEditor : UnityEditor.Editor
    {
        private readonly DownwashCurvePanel panel=new DownwashCurvePanel();
        private float previewHeight=1.9f;
        public override void OnInspectorGUI()
        {
            previewHeight=EditorGUILayout.FloatField("Preview fan height (m)",previewHeight);
            EditorGUI.BeginChangeCheck();float scrub=EditorGUILayout.Slider("Scrub height",previewHeight,.25f,8);
            if(EditorGUI.EndChangeCheck())previewHeight=scrub;
            panel.Draw((DownwashResponseProfile)target,previewHeight);
        }
    }

    [CustomEditor(typeof(GraceOriginalVWaterFanBridge))]
    public sealed class GraceOriginalVWaterFanBridgeEditor : UnityEditor.Editor
    {
        private readonly DownwashCurvePanel panel=new DownwashCurvePanel();
        private bool showSettings;
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.LabelField("Downwash height response",EditorStyles.boldLabel);
            var height=serializedObject.FindProperty("fanHeight");
            EditorGUILayout.PropertyField(height,new GUIContent("Fan height (m)"));
            EditorGUI.BeginChangeCheck();float scrub=EditorGUILayout.Slider("Scrub height (live in Play)",height.floatValue,.25f,8);
            if(EditorGUI.EndChangeCheck())height.floatValue=scrub;
            EditorGUILayout.PropertyField(serializedObject.FindProperty("useHeightResponse"),new GUIContent("Use height curves"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("responseProfile"));
            serializedObject.ApplyModifiedProperties();
            var bridge=(GraceOriginalVWaterFanBridge)target;
            if(bridge.responseProfile!=null)panel.Draw(bridge.responseProfile,bridge.fanHeight);
            else EditorGUILayout.HelpBox("Assign a Height Response Profile asset to author curves. Create one with Assets > Create > Grace > VWater > Height Response Profile.",MessageType.Info);
            showSettings=EditorGUILayout.Foldout(showSettings,"Manual settings, effect options and scene bindings",true);
            if(showSettings)
            {
                serializedObject.Update();
                DrawPropertiesExcluding(serializedObject,"m_Script","fanHeight","useHeightResponse","responseProfile");
                serializedObject.ApplyModifiedProperties();
            }
            if(Application.isPlaying)Repaint();
        }
    }
}
