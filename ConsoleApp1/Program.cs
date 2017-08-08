using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static ConsoleApp1.NewFeaturesInCShar6;

namespace ConsoleApp1
{
    class Program
    {
        static void Main(string[] args)
        {
            var temp = GetInstance();
            var temp2 = temp?.First;
            //var temp3 = temp?[1];
            var (_, abc) = new NewFeturesInCSharp7().TestFunc1(1, "dsadas");
            var ss = new NewFeturesInCSharp7();
            var s = ss.GetA();
            s += 5;
            var s2 = ss.a;
            var s3 = ss.GetA2();
            s3 += 444;
            var s4 = ss.a;
            ref var s5 = ref ss.GetA2();
            s5 += 10;
            var s6 = ss.a;
        }
    }
    public class NewFeaturesInCShar6
    {
        public string First { get; set; } = "this is first";
        public string Second { get; } = "this is readonlySecond";
        public string GetFirstAndSecond() => First + Second;
        public static string operator +(NewFeaturesInCShar6 left, NewFeaturesInCShar6 right) => $"{nameof(NewFeaturesInCShar6)}{left.First}:{left.Second}-----{right.First}:{right.Second}";
        private List<string> list { get; } = new List<string>();
        public string this[int index] => this.list[index];
        public static NewFeaturesInCShar6 GetInstance() => new NewFeaturesInCShar6();
        public Dictionary<string, string> dic = new Dictionary<string, string>()
        {
            ["123"] = "123",
            ["3444"] = "sadasdas"
        };
        public NewFeaturesInCShar6()
        {
            try { }
            catch(Exception ex) when(ex is ArgumentException)
            {

            }
        }
    }
    public class NewFeturesInCSharp7
    {
        public (string first, string second) FTuple { get; set; } = ("abc", "bbc");
        public (int first, string second) TestFunc1(int a, string b) => (a + 1, b + 1 + $"{a + 10}");
        private Dictionary<string, string> dic = new Dictionary<string, string>();
        public string PatternTest(Object unkonwData)
        {
            string a = "";
            if (unkonwData is string dt1)
                a += dt1;
            if (unkonwData is ValueTuple<string, string> t2)
                a += t2.Item1;
            if(!dic.TryGetValue("asd", out string resu))
            {
                resu = "asds";
            }
            a += resu;
            return a;
        }

        public int a = 21;
        public int GetA() => a;
        public ref int GetA2() => ref a;
    }
}
