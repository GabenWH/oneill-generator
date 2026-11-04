using UnityEngine;

namespace Grace.VWaterOriginal
{
    /// <summary>Optional CAMERA motion only. Original water stays world-Y.</summary>
    public sealed class GraceOriginalVWaterCameraOrbit : MonoBehaviour
    {
        public Transform target;
        public bool autoOrbit;
        private float yaw,pitch,distance;
        private GraceOriginalVWaterFanBridge controls;
        private void Start()
        {
            controls=target!=null?target.GetComponentInParent<GraceOriginalVWaterFanBridge>():null;
            Vector3 d=transform.position-(target!=null?target.position:Vector3.zero);distance=d.magnitude;
            yaw=Mathf.Atan2(d.x,d.z);pitch=Mathf.Asin(d.y/Mathf.Max(distance,.001f));
        }
        private void LateUpdate()
        {
            if(autoOrbit)yaw+=Time.deltaTime*.18f;
            Apply();
        }
        private void OnGUI()
        {
            Event e=Event.current;
            Rect panel=controls!=null?controls.ControlPanelRect:new Rect(12,12,590,310);
            if(panel.Contains(e.mousePosition))return;
            // IMGUI events avoid changing the project's Active Input Handling.
            if(e.type==EventType.MouseDrag&&e.button==1){yaw+=e.delta.x*.005f;pitch=Mathf.Clamp(pitch-e.delta.y*.005f,.15f,1.2f);autoOrbit=false;e.Use();Apply();}
            if(e.type==EventType.ScrollWheel){distance=Mathf.Clamp(distance*Mathf.Exp(e.delta.y*.05f),5,14);e.Use();Apply();}
        }
        private void Apply()
        {
            if(distance<=0)return;
            Vector3 center=target!=null?target.position:Vector3.zero;
            transform.position=center+new Vector3(Mathf.Sin(yaw)*Mathf.Cos(pitch),Mathf.Sin(pitch),Mathf.Cos(yaw)*Mathf.Cos(pitch))*distance;
            transform.LookAt(center,Vector3.up);
        }
    }
}
