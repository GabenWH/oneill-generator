"""Execute production math bodies using a documented C# -> C++ syntax adapter.
This is numerical verification, NOT C#/Unity type checking or API validation.
"""
import pathlib, re, subprocess, tempfile, unittest
ROOT=pathlib.Path(__file__).resolve().parents[2]
class OrbitMathTests(unittest.TestCase):
    def test_production_math(self):
        p=ROOT/'Assets/GraceLunarOrbit/Runtime/LunarOrbitMath.cs'
        self.assertTrue(p.exists(), 'production orbit math is not implemented')
        source=p.read_text()
        source=source.replace('using System;', '')
        source=re.sub(r'namespace GraceLunarOrbit\s*\{', '', source, count=1)
        source=source.rsplit('}',1)[0]
        source=source.replace('public static class LunarOrbitMath','struct LunarOrbitMath')
        source=source.replace('public const double','static constexpr double')
        source=source.replace('public static double','static double')
        for cs,cpp in {'Math.PI':'M_PI','Math.Sqrt':'std::sqrt','Math.Pow':'std::pow',
            'Math.Cos':'std::cos','Math.Sin':'std::sin','Math.Acos':'std::acos',
            'Math.Asin':'std::asin','Math.Min':'std::min','Math.Max':'std::max',
            'Math.Abs':'std::abs','Math.Floor':'std::floor',
            'double.IsNaN':'std::isnan','double.IsInfinity':'std::isinf'}.items():
            source=source.replace(cs,cpp)
        source=re.sub(r'throw new ArgumentOutOfRangeException\([^;]+;',
                      'throw std::invalid_argument("invalid");',source)
        source=source.rstrip()+';\n'
        cases=r'''
int checks=0;
void near(double a,double b,double eps) { ++checks; if(std::abs(a-b)>eps || !std::isfinite(a)) throw std::runtime_error("numeric assertion"); }
int main() {
 using M=LunarOrbitMath;
 // Independent lunar fixtures calculated from NASA constants.
 near(M::OrbitRadius(1737400,100000),1837400,1e-8);
 near(M::AngularSpeed(4.9048695e12,1837400),0.0008892178261388336,1e-12);
 near(M::OrbitalPeriod(4.9048695e12,1837400),7065.968677733854,0.01);
 near(M::SpinRate(9.81,4000),0.049522722,1e-9);
 near(M::SpinRate(0,4000),0,1e-12);
 near(M::Angle(0,1,2),2,1e-12);
 near(M::Angle(0,1,2*M_PI),0,1e-12);
 near(M::Angle(0,-1,1),2*M_PI-1,1e-12);
 near(M::Angle(1,0,1e12),1,1e-12);
 near(M::OrbitX(10,0),10,1e-12); near(M::OrbitY(10,0),0,1e-12);
 near(M::OrbitX(10,M_PI/2),0,1e-12); near(M::OrbitY(10,M_PI/2),10,1e-12);
 near(M::SunVisibility(0,0.5,0.01),0,1e-12);
 near(M::SunVisibility(1,0.5,0.01),1,1e-12);
 near(M::SunVisibility(0,0.01,0.5),0.9996,1e-10);
 double half=M::SunVisibility(0.5,0.5,0.01);
 if(half<0.49 || half>0.51) throw std::runtime_error("partial eclipse");
 ++checks;
 for(int i=0;i<1000;++i) { double a=i*0.01; double x=M::OrbitX(1837400,a), y=M::OrbitY(1837400,a); near(std::sqrt(x*x+y*y),1837400,1e-6); }
 near(M::MoonFillWeight(std::acos(0.8),1),0.2,1e-12);
 near(M::MoonFillWeight(std::acos(0.8),0),0.1,1e-12);
 near(M::MoonFillWeight(std::acos(0.8),-1),0,1e-12);
 int invalid=0;
 try { M::OrbitRadius(1,-1); } catch(std::invalid_argument&) {++invalid;}
 try { M::AngularSpeed(0,1); } catch(std::invalid_argument&) {++invalid;}
 try { M::SpinRate(-1,1); } catch(std::invalid_argument&) {++invalid;}
 try { M::SpinRate(1,0); } catch(std::invalid_argument&) {++invalid;}
 try { M::OrbitRadius(NAN,1); } catch(std::invalid_argument&) {++invalid;}
 try { M::Angle(0,1,NAN); } catch(std::invalid_argument&) {++invalid;}
 if(invalid!=6) throw std::runtime_error("invalid input accepted");
 std::cout << checks << " production-math numerical assertions + 6 invalid-input cases passed\n";
}
'''
        with tempfile.TemporaryDirectory() as tmp:
            cpp=pathlib.Path(tmp)/'checks.cpp'; exe=pathlib.Path(tmp)/'checks'
            cpp.write_text('#include <cmath>\n#include <algorithm>\n#include <stdexcept>\n#include <iostream>\n'+source+cases)
            subprocess.run(['g++','-std=c++17','-Wall','-Wextra','-Werror',str(cpp),'-o',str(exe)],check=True)
            subprocess.run([str(exe)],check=True)
if __name__=='__main__': unittest.main()
