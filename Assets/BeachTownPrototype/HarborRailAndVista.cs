using UnityEngine;

namespace CylinderBeachTown
{
    public sealed partial class BeachTownPrototype
    {
        private void BuildRail()
        {
            for (int k=-1;k<=1;k+=2)
                Box("Steel running rail",47,.064f,RailZ+k*.7175f,115,.064f,.074f,"6c6961");
            for (int i=0;i<127;i++)
                Box("Timber sleeper",-10.5f+i*.9f,.028f,RailZ,.18f,.052f,1.92f,"82644f");
            Stop(StopA,"HARBOR LOCAL", "HARBOR BEACH");
            Stop(StopB,"HARBOR LOCAL", "FIELD OVERLOOK");
            tram=new GameObject("T01 Harbor Local — scripted shuttle").transform;
            tram.SetParent(district,false); tram.localPosition=new Vector3(StopA,RailY,RailZ);
            if (!NormalizeTram()) BuildFallbackTram();
        }

        private void Stop(float x,string line,string name)
        {
            float z=RailZ+3.75f;
            Box("Level platform",x,.09f,z,15,.18f,3.8f,"ddc59a",true);
            Box("Ochre tactile edge",x,.184f,z-1.78f,14.9f,.018f,.2f,"c79a54");
            for (int i=-1;i<=1;i+=2)
                Box("Shelter post",x+5+i*1.45f,1.38f,z+.9f,.09f,2.76f,.09f,"5a7965");
            Box("Shelter canopy",x+5,2.78f,z+.68f,3.65f,.16f,1.67f,"6e8774");
            Box("Stop sign board",x-4,2.29f,z+1.04f,4,.68f,.11f,"5f7b68");
            Box("Stop sign poles",x-4,1.0f,z+1.04f,.085f,2.05f,.085f,"4d6453");
            Sign(line,x-4,2.42f,z+.974f,180,.31f,"f4dda0");
            Sign(name,x-4,2.15f,z+.972f,180,.26f,"ecdbac");
            Sign(name,x-4,2.28f,z+1.105f,0,.36f,"f2e3b2");
        }

        private bool NormalizeTram()
        {
            if (harborLocalModel==null) return false;
            var wrap=new GameObject("Meter-normalized imported exterior").transform;
            wrap.SetParent(tram,false);
            var instance=Instantiate(harborLocalModel,wrap);
            instance.name="Harbor Local FBX";
            var renderers=instance.GetComponentsInChildren<Renderer>();
            if (renderers.Length==0) { wrap.gameObject.SetActive(false); return false; }
            Bounds b = MergedBounds(renderers);
            if (b.size.z>b.size.x) { wrap.localRotation=Quaternion.Euler(0,90,0); b=MergedBounds(renderers); }
            if (b.size.x<.01f || b.size.y<.01f || b.size.z<.01f || b.size.y>=b.size.x || b.size.z>=b.size.x)
            { wrap.gameObject.SetActive(false); return false; }
            // Snap the finished exterior bounds to the running rails, preserving the model's proportions.
            wrap.localScale = Vector3.one*(11.29f/b.size.x);
            b=MergedBounds(renderers);
            if (b.size.y<2.7f || b.size.y>4.7f || b.size.z<2.1f || b.size.z>3.4f)
            { wrap.gameObject.SetActive(false); return false; }
            wrap.position += new Vector3(tram.position.x-b.center.x,tram.position.y-b.min.y,tram.position.z-b.center.z);
            foreach (var c in instance.GetComponentsInChildren<Collider>()) c.enabled=false;
            return true;
        }

        private static Bounds MergedBounds(Renderer[] rs)
        {
            Bounds b=rs[0].bounds;
            for (int i=1;i<rs.Length;i++) b.Encapsulate(rs[i].bounds);
            return b;
        }

        private void BuildFallbackTram()
        {
            Box("Prototype wheel frame",0,.42f,0,10,.42f,1.89f,"55534a",false,tram);
            Box("Prototype lower body",0,1.03f,0,11.2f,1.05f,2.64f,"638477",false,tram);
            Box("Prototype window band",0,2.12f,0,10.92f,1.23f,2.55f,"e3d19e",false,tram);
            Box("Prototype roof panel",0,2.83f,0,10.79f,.26f,2.55f,"928c74",false,tram);
            Box("Prototype air housing",0,3.04f,0,3.7f,.24f,1.6f,"928c74",false,tram);
            for(int side=-1;side<=1;side+=2)
                for (int i=-4;i<=4;i++)
                    Box("Prototype tram glazing",i*1.14f,2.14f,side*1.286f,.89f,.80f,.024f,"47615e",false,tram);
            for (int side=-1;side<=1;side+=2)
                Box("Prototype end windshield",side*5.469f,2.11f,0,.027f,.8f,2.0f,"47615e",false,tram);
        }

        private static Vector3 CylinderPoint(int i,int row,int side)
        {
            float angle = row*.103f;
            const float r = 600;
            float hill = Mathf.Sin(i*.88f+row*.23f)*16 + Mathf.Sin(i*.28f-row*.56f)*21;
            return new Vector3((i-9)*82 + 35,-14+r*(1-Mathf.Cos(angle)) + hill*Mathf.Min(row*.27f,1),
                side*((side>0 ? 105 : 285) + r*Mathf.Sin(angle)));
        }
        private void BuildCylinderBackdrop()
        {
            string[] fields={"9da877","abb17c","bac08a","8d9f78","b5b890","8ba784","a2b68e"};
            const int rows = 24, across=18;
            for (int side=-1;side<=1;side+=2)
            {
                // Combine nonadjacent faces by material while retaining a normal per flat triangle.
                for (int c=0;c<fields.Length;c++)
                {
                    var verts=new System.Collections.Generic.List<Vector3>();
                    var tris=new System.Collections.Generic.List<int>();
                    for(int j=0;j<rows;j++)
                        for(int i=0;i<across;i++)
                        {
                            int shade=((i/2)*13+(j/2)*11+i*j/3+(side+1))%fields.Length;
                            if (shade!=c) continue;
                            Vector3 a=CylinderPoint(i,j,side),b=CylinderPoint(i+1,j,side);
                            Vector3 d=CylinderPoint(i,j+1,side),e=CylinderPoint(i+1,j+1,side);
                            int start=verts.Count;
                            if (side==1) verts.AddRange(new[]{a,d,b,b,d,e});
                            else verts.AddRange(new[]{a,b,d,b,e,d});
                            for (int k=0;k<6;k++) tris.Add(start+k);
                        }
                    MeshObject("Rising patchwork — distant non-playable cylinder",verts.ToArray(),tris.ToArray(),fields[c]);
                }
            }
        }
    }
}
