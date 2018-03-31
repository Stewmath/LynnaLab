using NUnit.Framework;
using System;
using System.Collections.Generic;

namespace LynnaLab
{
    public class NUnitTestClass
    {
        public static void RunTests() {
            new NUnitTestClass().TestCase ();
        }

        [Test ()]
        public void TestCase ()
        {
            TestTokenizer("Test line",
                    new List<string>{"Test", "line"},
                    new List<string>{""," ",""});

            TestTokenizer("Longer   Test    MoreSpace\t",
                    new List<string>{"Longer", "Test", "MoreSpace"},
                    new List<string>{"","   ","    ","\t"});

            TestTokenizer("/*Try some   */Comments",
                    new List<string>{"Comments"},
                    new List<string>{"/*Try some   */",""});

            TestTokenizer(" \t  Testing   /*  aoe*/ testor /**/a",
                    new List<string>{"Testing","testor","a"},
                    new List<string>{" \t  ","   /*  aoe*/ "," /**/",""});

            TestTokenizer("somestuffandthen ; nothing arcuh.,c. */ aoe",
                    new List<string>{"somestuffandthen"},
                    new List<string>{""," ; nothing arcuh.,c. */ aoe"});

	        TestTokenizer("\tm_InteractionData $00 $00 $80",
                    new List<string>{"m_InteractionData","$00","$00","$80"},
                    new List<string>{"\t"," "," "," ",""});
        }

        void TestTokenizer(string input, IList<string> tokens, IList<string> spacing) {
            var tup = FileParser.Tokenize (input);
            List<string> actualTokens = tup.Item1;
            List<string> actualSpacing = tup.Item2;

            Assert.AreEqual (tokens, actualTokens);
            Assert.AreEqual (spacing, actualSpacing);
        }
    }
}
