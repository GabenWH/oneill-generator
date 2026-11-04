"""Independent model of slot coalescing and foam recurrence. Does not execute C#/GPU."""
import unittest
class Pending:
 def __init__(self): self.incoming={}; self.pending={}; self.dispatches=[]
 def add(self,slot,x):
  if not 0<=slot<4:return False
  self.incoming[slot]=x;return True
 def update(self): self.pending.update(self.incoming);self.incoming.clear()
 def dispatch(self): self.dispatches.append(self.pending.copy());self.pending.clear()
 def clear(self): self.incoming.clear();self.pending.clear()
class TimingTests(unittest.TestCase):
 def test_short_input_survives_empty_frames_exactly_once(self):
  p=Pending();p.add(0,'a');p.update();p.update();p.update();p.dispatch();p.update();p.dispatch();self.assertEqual(p.dispatches,[{0:'a'},{}])
 def test_repeated_inputs_coalesce_with_low_sim_rate(self):
  p=Pending()
  for i in range(20):p.add(0,i);p.add(1,-i);p.update()
  p.dispatch();p.dispatch();self.assertEqual(p.dispatches,[{0:19,1:-19},{}])
 def test_stable_slots_do_not_shift_when_fan_stops(self):
  p=Pending();p.add(0,'old0');p.add(1,'old1');p.update();p.add(1,'new1');p.update();p.dispatch()
  self.assertEqual(p.dispatches,[{0:'old0',1:'new1'}])
 def test_culling_or_disable_clears_both_stages(self):
  p=Pending();p.add(0,'pending');p.update();p.add(1,'incoming');p.clear();p.update();p.dispatch();self.assertEqual(p.dispatches,[{}])
 def test_missing_camera_clears_before_reappearance(self):
  p=Pending();p.add(0,'old');p.update();p.add(1,'queued');p.clear();p.update();p.dispatch();self.assertEqual(p.dispatches,[{}])
 def test_dispatch_faster_than_render_does_not_replay(self):
  p=Pending();p.add(0,'a');p.update();p.dispatch();p.dispatch();self.assertEqual(p.dispatches,[{0:'a'},{}])
 def test_fan_off_leaves_one_pending_then_clears(self):
  p=Pending();p.add(0,'a');p.update();p.update();p.dispatch();p.dispatch();self.assertEqual(p.dispatches,[{0:'a'},{}])
 def test_default_foam_accumulates_and_fades(self):
  dt=1/60;foam=0;input_strength=.55*(.9/(1+(1.9/1.2)**2))*.35
  for _ in range(600):foam=min(1,max(0,foam*(1-dt*.3))+dt*input_strength)
  self.assertGreater(foam,.1);self.assertLess(foam,.2)
  initial=foam
  for _ in range(300):foam=max(0,foam*(1-dt*.3))
  self.assertLess(foam,initial*.25);self.assertGreater(foam,0)
if __name__=='__main__':unittest.main(verbosity=2)
