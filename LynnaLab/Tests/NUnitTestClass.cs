using NUnit.Framework;
using NUnit.Framework.Legacy;
using System.Collections.Generic;

using LynnaLib;

namespace LynnaLab
{
    public class NUnitTestClass
    {
        public static void RunTests() {
            var tester = new NUnitTestClass();

            tester.TestTokenizer();
            tester.TestDocumentation();
        }

        [Test ()]
        public void TestTokenizer ()
        {
            // FileParser tokenizer
            TestTokenizerHlpr("Test line",
                    new List<string>{"Test", "line"},
                    new List<string>{""," ",""});

            TestTokenizerHlpr("Longer   Test    MoreSpace\t",
                    new List<string>{"Longer", "Test", "MoreSpace"},
                    new List<string>{"","   ","    ","\t"});

            TestTokenizerHlpr("/*Try some   */Comments",
                    new List<string>{"Comments"},
                    new List<string>{"/*Try some   */",""});

            TestTokenizerHlpr(" \t  Testing   /*  aoe*/ testor /**/a",
                    new List<string>{"Testing","testor","a"},
                    new List<string>{" \t  ","   /*  aoe*/ "," /**/",""});

            TestTokenizerHlpr("somestuffandthen ; nothing arcuh.,c. */ aoe",
                    new List<string>{"somestuffandthen"},
                    new List<string>{""," ; nothing arcuh.,c. */ aoe"});

	        TestTokenizerHlpr("\tm_InteractionData $00 $00 $80",
                    new List<string>{"m_InteractionData","$00","$00","$80"},
                    new List<string>{"\t"," "," "," ",""});
        }

        void TestTokenizerHlpr(string input, IList<string> tokens, IList<string> spacing) {
            var tup = FileParser.Tokenize (input);
            List<string> actualTokens = tup.Item1;
            List<string> actualSpacing = tup.Item2;

            ClassicAssert.AreEqual (tokens, actualTokens);
            ClassicAssert.AreEqual (spacing, actualSpacing);
        }

        void TestDocumentation() {
        }
    }
}
