using UnityEngine;
using UnityEngine.Rendering;

namespace CylinderBeachTown
{
    public sealed partial class BeachTownPrototype
    {
        private static Color Hex(string value)
        {
            return ColorUtility.TryParseHtmlString("#" + value, out Color c) ? c : Color.magenta;
        }
        private Material Mat(string hex, bool flat = false)
        {
            var key = flat ? hex + "-flat" : hex;
            if (materials.TryGetValue(key, out Material m)) return m;
            m = new Material(Shader.Find(flat ? "Unlit/Color" : "Standard"));
            m.color = Hex(hex);
            if (!flat) m.SetFloat("_Glossiness", .05f);
            materials.Add(key,m);
            return m;
        }
        private void ConfigureLight()
        {
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = Hex("d6e5dd");
            RenderSettings.ambientEquatorColor = Hex("b4bfab");
            RenderSettings.ambientGroundColor = Hex("a79670");
            RenderSettings.ambientIntensity = 1;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = Hex("b4d9dc");
            RenderSettings.fogDensity = .00123f;
            var go = new GameObject("Late afternoon sun");
            go.transform.SetParent(transform,false);
            go.transform.rotation = Quaternion.Euler(39,-42,0);
            var sun = go.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = Hex("fff0ca");
            sun.intensity = 1.10f;
            sun.shadows = LightShadows.Soft;
            RenderSettings.sun = sun;
        }
        private GameObject Box(string label, float x, float y, float z, float sx, float sy, float sz, string tone,
            bool solid = false, Transform parent = null, float turn = 0)
        {
            return Prim(PrimitiveType.Cube,label,new Vector3(x,y,z),new Vector3(sx,sy,sz),tone,solid,parent,turn);
        }
        private GameObject Prim(PrimitiveType type,string label,Vector3 p,Vector3 scale,string tone,
            bool solid=false,Transform parent=null,float turn=0)
        {
            var g = GameObject.CreatePrimitive(type);
            g.name = label; g.transform.SetParent(parent ? parent : district,false);
            g.transform.localPosition=p; g.transform.localScale=scale;
            g.transform.localRotation=Quaternion.Euler(0,turn,0);
            g.GetComponent<Renderer>().sharedMaterial=Mat(tone);
            if (!solid) g.GetComponent<Collider>().enabled=false;
            return g;
        }
        private void MeshObject(string name, Vector3[] verts, int[] triangles, string color, bool solid=false, bool flat=false)
        {
            var mesh = new Mesh {name=name, vertices=verts, triangles=triangles};
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            var go = new GameObject(name);
            go.transform.SetParent(district,false);
            go.AddComponent<MeshFilter>().sharedMesh=mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial=Mat(color,flat);
            if (solid) go.AddComponent<MeshCollider>().sharedMesh=mesh;
        }
        private Transform Group(string label)
        {
            var g=new GameObject(label).transform;
            g.SetParent(transform,false);
            district=g;
            return g;
        }
        private float Between(float a,float b) => a + (b-a)*(float)rng.NextDouble();

        private void BuildTown()
        {
            Group("01 — land, dunes and water");
            Box("Town slab",31,-1,39,219,2,142,"9ca967",true);
            BeachSection(-32,-43,0,-.52f,"e0c288");
            BeachSection(-43,-54,-.52f,-.94f,"e9ce96");
            Box("Water apron",29,-.88f,-298,1000,.25f,502,"448f8c");
            for (int band=0;band<4;band++)
                for (int i=0;i<16;i++)
                    Box("Surf glint",-72+i*13.1f+Between(-1,1),-.742f,-54-band*3.3f+Between(-1,1),
                        Between(3,11),.018f,Between(.15f,.60f),band%2==0?"b5d5c2":"75ada0");
            BuildCylinderBackdrop();

            Group("02 — boardwalk, streets and plaza");
            Box("Main street",31,.004f,-.8f,218,.012f,7.2f,"bab19b");
            Box("Promenade deck",31,.081f,-24,216,.16f,5.3f,"a27954",true);
            for (int i=0;i<132;i++)
                Box("Promenade plank seam",-75+i*1.63f,.165f,-24,.032f,.013f,5.14f,"785844");
            Pave(-6,8,27,22);
            Pave(-5,-8,6,22);
            Pave(-7,-30,6,10);
            Pave(94,-5,12,14);
            Pave(96,5,7,10);
            for (int i=0;i<13;i++)
            {
                int x=-63+i*14;
                Box("Low seaward rail",x,.68f,-26.5f,9.4f,.13f,.16f,"aa7350");
                for (int k=-1;k<=1;k+=2) Box("Promenade rail post",x+k*4.6f,.38f,-26.5f,.14f,.73f,.15f,"80634f");
                if (i%2==0) Lamp(x,-21.7f);
            }
            for (int i=0;i<8;i++) Lamp(-41+i*19.5f,3.4f);

            Group("03 — market square and storefronts");
            Box("Welcome sign board",-9,3.45f,7.45f,6.5f,1.04f,.18f,"58776b");
            Box("Welcome sign post",-11.85f,1.69f,7.47f,.15f,3.41f,.17f,"7b644f");
            Box("Welcome sign post",-6.15f,1.69f,7.47f,.15f,3.41f,.17f,"7b644f");
            Sign("HARBOR BEACH",-9,3.45f,7.346f,180,.72f,"fff1c5");
            Sign("HARBOR BEACH",-9,3.45f,7.556f,0,.72f,"fff1c5");
            Kiosk(-12,10,"MARKET","bd674a","f4dc9d");
            Kiosk(5,11,"PIER CAFE","749382","f8d9a2");
            Store(-26,8,14,7,7,"d9a66a","a85542","GENERAL STORE");
            Store(22,8,13,7,7,"dbbc86","5a8070","BICYCLE CO-OP");
            Store(37,7.9f,10,7,7,"d3cc9b","7a7352","POST OFFICE");
            Store(-40,8,12,7,8,"b67d66","795947","COASTAL BOOKS");
            Store(5,24,12,7,7,"c78b64","825847","GUEST HOUSE");
            for(int i=0;i<4;i++)
            {
                Box("Planter wall",-16+i*6.2f,.24f,18,2.2f,.48f,.8f,"bb9c70",true);
                for(int j=0;j<3;j++)
                    Shrub(-16+i*6.2f+(j-1)*.65f,.48f,18,.46f,j%2==0?"657f4d":"a28654");
            }
            Bench(-16,6.4f,90); Bench(6,6.4f,90); Bench(-1,-22.2f); Bench(98,11);

            Group("04 — cottages and gardens");
            string[] plaster = {"d6a16c","c7b28b","789b85","c5927c","e1cea2","abb67b"};
            for (int row=0;row<3;row++)
            {
                for(int i=0;i<10;i++)
                {
                    float x=-64+i*15.0f+Between(-1.3f,1.3f);
                    float z=29+row*19+Between(-2,2);
                    if (row==0 && x>-7 && x<13) continue;
                    House(x,z,Between(6,8),Between(2.5f,3.8f),Between(5,7),plaster[(row+i)%plaster.Length],i%2==0?"976548":"76634e");
                    Tree(x+4,z+4,Between(3.5f,6.5f),i%2==0?"7d914f":"899557");
                }
            }
            for (int i=0;i<90;i++)
            {
                float x=Between(-72,134),z=Between(69,103);
                if (x>82 && x<111) continue;
                if (i%4==0) Tree(x,z,Between(2.3f,6.2f),"789350");
                else Shrub(x,0,z,Between(.32f,.7f),i%2==0?"829b60":"b1ac5e");
            }
            for (int i=0;i<11;i++)
                Box("Allotment row",110+i*1.85f,.125f,36,1.16f,.24f,45,i%2==0?"839051":"ac985f");
            Pave(96,46,3,63);
            for(int i=0;i<9;i++) Tree(104,35+i*6.8f,Between(2.4f,4.2f),"728650");
            BuildOverlook();
            for(int i=0;i<17;i++)
            {
                float x=-68+i*12.5f;
                Shrub(x,-.26f,-39,Between(.25f,.85f),"a3a478");
                if (i%4==0) BeachUmbrella(x,-34.5f,i%8==0?"ca6b48":"588e80");
            }
            Sign("SOUTH BEACH",-7,2.6f,-33.8f,180,.52f,"f5e9bb");
            checkpoints.Add(new Vector3(-6,0,8.5f));
            checkpoints.Add(new Vector3(-7,-.1f,-37));
            checkpoints.Add(new Vector3(95,2.8f,23.5f));

            Group("05 — Harbor Local service");
            BuildRail();
        }

        private void BuildOverlook()
        {
            RampStrip(89,102,10,21,.01f,2.8f);
            RampStrip(89,102,27,39,2.8f,.01f);
            Box("Overlook deck",95.5f,2.7f,24,13,.2f,6,"cdbb8e",true);
            // Earth retaining faces close the sides where the paths rise above the meadow.
            foreach (float x in new[]{89f,102f})
            {
                bool left = x < 90;
                MeshObject("Overlook retaining face",new[]{new Vector3(x,.0f,10),new Vector3(x,2.8f,21),
                    new Vector3(x,.0f,21),new Vector3(x,2.8f,27),new Vector3(x,.0f,27),new Vector3(x,0,39)},
                    left ? new[]{0,1,2,1,3,2,2,3,4,4,3,5} : new[]{0,2,1,1,2,3,2,4,3,4,5,3},"94945e");
                Box("Lookout handrail",x,3.66f,24,.11f,.13f,6,"826e54",true);
                for(int i=0;i<3;i++) Box("Lookout post",x,3.22f,21.1f+i*2.9f,.12f,.84f,.13f,"826e54",true);
            }
            Sign("FIELD OVERLOOK",95,4.37f,24.1f,180,.46f,"f8dd9f");
            Box("Lookout plate",95,4.36f,24.19f,5.15f,.67f,.12f,"657c65");
            Box("Lookout plate support",95,3.45f,24.21f,.13f,1.43f,.13f,"765e44");
        }

        private void RampStrip(float x0, float x1, float z0, float z1, float y0, float y1)
        {
            MeshObject("Overlook footpath ramp",new[]{new Vector3(x0,y0,z0),new Vector3(x1,y0,z0),
                new Vector3(x0,y1,z1),new Vector3(x1,y1,z1)},new[]{0,2,1,1,2,3},"b09773",true);
        }

        private void BeachSection(float z0,float z1,float y0,float y1,string col)
        {
            MeshObject("Walking beach",new [] {new Vector3(-78,y0,z0),new Vector3(140,y0,z0),
                new Vector3(-78,y1,z1),new Vector3(140,y1,z1)},new[]{0,1,2,1,3,2},col,true);
        }
        private void Pave(float x,float z,float w,float d)
        {
            Box("Walkway",x,.012f,z,w,.02f,d,"d0bc95");
            for(int i=0;i<(int)(w*1.3f);i++)
                Box("Paver seam",x-w/2+.18f+i*.77f,.024f,z,.022f,.013f,d-.1f,"b9a681");
        }
        private void Roof(float x,float y,float z,float w,float h,float d,string color)
        {
            float a=w/2,b=d/2;
            var v=new [] {
                new Vector3(x-a,y,z-b), new Vector3(x+a,y,z-b),
                new Vector3(x-a,y+h,z), new Vector3(x+a,y+h,z),
                new Vector3(x-a,y,z+b), new Vector3(x+a,y,z+b)
            };
            int[] t={0,2,1,1,2,3,2,4,3,3,4,5,0,4,2,1,3,5};
            var p=new Vector3[t.Length];for(int i=0;i<p.Length;i++)p[i]=v[t[i]];
            var ix=new int[t.Length];for(int i=0;i<ix.Length;i++)ix[i]=i;
            MeshObject("Hard-gabled roof",p,ix,color);
        }
        private void Store(float x,float z,float w,float h,float d,string body,string trim,string title)
        {
            h*=.73f;
            Box("Commercial plaster block",x,h/2,z,w,h,d,body,true);
            Box("Roof parapet",x,h+.13f,z,w+.3f,.26f,d+.3f,trim);
            float f=z-d/2;
            Box("Dark entry",x,1.16f,f-.025f,1.3f,2.31f,.065f,"434b45");
            for (int k=-1;k<=1;k+=2)
            {
                Box("Display window surround",x+k*w*.27f,1.66f,f-.03f,w*.36f,1.61f,.065f,trim);
                Box("Display window glass",x+k*w*.27f,1.66f,f-.073f,w*.32f,1.43f,.054f,"45645e");
                Box("Glazing highlight",x+k*w*.27f,1.33f,f-.106f,w*.28f,.18f,.018f,"86a099");
            }
            Box("Canvas awning",x,2.85f,f-.65f,w-.17f,.22f,1.38f,trim);
            for (float i=-w/2+.35f;i<w/2-.2f;i+=.82f)
                Box("Canvas stripe",x+i,2.968f,f-.65f,.36f,.02f,1.38f,"e8d4a6");
            Sign(title,x,3.96f,f-.11f,180,.44f,"fff0c7");
        }
        private void Kiosk(float x,float z,string label,string body,string cap)
        {
            Box("Concourse kiosk",x,1.42f,z,3.5f,2.84f,2.8f,body,true);
            Box("Kiosk counter opening",x,1.47f,z-1.405f,2.65f,1.08f,.035f,"384c45");
            Box("Serving ledge",x,.99f,z-1.6f,3,.14f,.5f,cap);
            Roof(x,2.92f,z,4.3f,.63f,3.65f,cap);
            Sign(label,x,2.55f,z-1.43f,180,.39f,"fff0c0");
        }
        private void House(float x,float z,float w,float h,float d,string body,string cover)
        {
            Box("Painted timber cottage",x,h/2,z,w,h,d,body,true);
            Roof(x,h,z,w+.55f,1.28f,d+.65f,cover);
            Box("Recessed door",x,.99f,z-d/2-.025f,.84f,1.97f,.06f,cover);
            for(int i=-1;i<=1;i+=2)
            {
                Box("Sash frame",x+i*w*.29f,1.65f,z-d/2-.025f,1.55f,1.22f,.08f,"f1dbac");
                Box("Sash glass",x+i*w*.29f,1.65f,z-d/2-.088f,1.32f,1.00f,.068f,"48665f");
                Box("Mullion",x+i*w*.29f,1.65f,z-d/2-.135f,.095f,1.03f,.04f,"e0c397");
            }
            Box("Porch step",x,.075f,z-d/2-.74f,1.4f,.15f,1.48f,"a58d71",true);
            Box("Chimney",x+w*.31f,h+.91f,z+.5f,.58f,1.35f,.63f,"9e6552");
        }
        private void Sign(string label,float x,float y,float z,float turn,float size,string tone)
        {
            var g=new GameObject("Sign — "+label);
            g.transform.SetParent(district,false); g.transform.localPosition=new Vector3(x,y,z);
            // TextMesh's visible face points toward local negative Z.
            g.transform.localRotation=Quaternion.Euler(0,turn+180,0);
            var t=g.AddComponent<TextMesh>();
            t.text=label; t.anchor=TextAnchor.MiddleCenter; t.alignment=TextAlignment.Center;
            t.fontSize=60;t.characterSize=size/5f;t.fontStyle=FontStyle.Bold;t.color=Hex(tone);
        }
        private void Lamp(float x,float z)
        {
            Box("Streetlamp mast",x,1.7f,z,.11f,3.4f,.12f,"534c3d");
            Box("Streetlamp cap",x,3.45f,z,.78f,.12f,.43f,"b78651");
            Box("Streetlamp glass",x,3.29f,z,.61f,.23f,.34f,"fff1b7");
        }
        private void Bench(float x,float z,float turn=0)
        {
            var g=new GameObject("Slatted timber bench").transform;
            g.SetParent(district,false);g.localPosition=new Vector3(x,0,z);g.localRotation=Quaternion.Euler(0,turn,0);
            Box("Seat",0,.49f,0,1.95f,.13f,.55f,"9f7150",true,g);
            Box("Backrest",0,.75f,.25f,1.95f,.32f,.1f,"b3875b",false,g);
            Box("Bracket",-.69f,.25f,0,.11f,.5f,.46f,"645751",false,g);
            Box("Bracket",.69f,.25f,0,.11f,.5f,.46f,"645751",false,g);
        }
        private void Tree(float x,float z,float tall,string tone)
        {
            Box("Trunk",x,tall*.23f,z,tall*.069f,tall*.5f,tall*.075f,"80684b",true);
            Facet(x,tall*.69f,z,tall*.35f,tall*.42f,tall*.29f,tone,7);
            Facet(x-tall*.13f,tall*.64f,z+tall*.09f,tall*.23f,tall*.27f,tall*.23f,"8a9b5a",6);
        }
        private void Shrub(float x,float y,float z,float size,string tone) => Facet(x,y+size*.44f,z,size,size*.72f,size*.83f,tone,5);
        private void Facet(float x,float y,float z,float rx,float ry,float rz,string tone,int sides)
        {
            var v=new Vector3[sides*6];var idx=new int[v.Length];
            for(int i=0;i<sides;i++)
            {
                float a=i*Mathf.PI*2/sides,b=(i+1)*Mathf.PI*2/sides;
                var p=new Vector3(x+Mathf.Cos(a)*rx,y,z+Mathf.Sin(a)*rz);
                var q=new Vector3(x+Mathf.Cos(b)*rx,y,z+Mathf.Sin(b)*rz);
                v[i*6]=p;v[i*6+1]=new Vector3(x-rx*.14f,y+ry,z);v[i*6+2]=q;
                v[i*6+3]=q;v[i*6+4]=new Vector3(x,y-ry*.49f,z);v[i*6+5]=p;
            }
            for(int i=0;i<idx.Length;i++)idx[i]=i;
            MeshObject("Faceted crown",v,idx,tone);
        }
        private void BeachUmbrella(float x,float z,string color)
        {
            Box("Umbrella pole",x,.88f,z,.062f,1.98f,.062f,"b48b5d");
            Facet(x,1.77f,z,1.28f,.25f,1.26f,color,7);
        }
    }
}
