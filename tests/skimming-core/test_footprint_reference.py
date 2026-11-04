import math,random,unittest
# Independent CPU transcription of HLSL footprint; tests numeric shape, not GPU.
def skim(x,z,vx,vz,r):
 speed=math.hypot(vx,vz);f=(vx/speed,vz/speed) if speed>.0001 else (1.,0.)
 along=x*f[0]+z*f[1]; across=-x*f[1]+z*f[0]; q=min(speed/1,1)
 length=r*(1+2*q if along<0 else 1)
 d=math.hypot(along/length,across/r); t=max(0,min(d,1)); mask=1-(t*t*(3-2*t));trail=max(0,min(-along/r,1))*q
 ripple=math.sin(-along/r*2*math.pi)
 return mask*(1+.45*trail*ripple), mask*(.35+.65*trail)
class NumericTest(unittest.TestCase):
 def test_stationary_circle(self):
  self.assertAlmostEqual(skim(.5,0,0,0,1)[0],skim(0,.5,0,0,1)[0]);self.assertEqual(skim(2,0,0,0,1),(0,0))
 def test_trailing_wake_points_behind_velocity(self):
  self.assertGreater(skim(-1.5,0,4,0,1)[0],0);self.assertEqual(skim(1.5,0,4,0,1)[0],0)
  self.assertGreater(skim(1.5,0,-4,0,1)[0],0)
 def test_demo_speed_reaches_beyond_hover_radius(self):
  self.assertGreater(skim(-1.2,0,.3,0,1)[0],0)
  self.assertEqual(skim(1.2,0,.3,0,1)[0],0)
 def test_foam_and_force_finite_bounded(self):
  random.seed(47)
  for _ in range(10000):
   p=[random.uniform(-30,30) for _ in range(4)];r=random.uniform(.02,10)
   a,b=skim(*p,r);self.assertTrue(math.isfinite(a) and math.isfinite(b));self.assertGreaterEqual(a,0);self.assertLessEqual(a,1.45);self.assertGreaterEqual(b,0);self.assertLessEqual(b,1)
if __name__=='__main__': unittest.main(verbosity=2)
