using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ShortWaveTrader.ThirdParty
{
    public static class MiniJSON
    {
        public static object Deserialize(string json)
        {
            return Parser.Parse(json);
        }

        sealed class Parser
        {
            StringReader r;
            Parser(string s){r=new StringReader(s);}
            public static object Parse(string s){return new Parser(s).ParseValue();}
            object ParseValue()
            {
                Eat();
                int c=r.Peek();
                if(c=='{')return ParseObject();
                if(c=='[')return ParseArray();
                if(c=='"')return ParseString();
                if(char.IsDigit((char)c)||c=='-')return ParseNumber();
                return null;
            }
            Dictionary<string,object> ParseObject()
            {
                var d=new Dictionary<string,object>(); r.Read();
                while(true){Eat(); if(r.Peek()=='}'){r.Read(); break;}
                    string k=ParseString(); Eat(); r.Read();
                    d[k]=ParseValue(); Eat();
                    if(r.Peek()==','){r.Read(); continue;}
                    if(r.Peek()=='}'){r.Read(); break;}
                } return d;
            }
            List<object> ParseArray()
            {
                var l=new List<object>(); r.Read();
                while(true){Eat(); if(r.Peek()==']'){r.Read(); break;}
                    l.Add(ParseValue()); Eat();
                    if(r.Peek()==','){r.Read(); continue;}
                    if(r.Peek()==']'){r.Read(); break;}
                } return l;
            }
            string ParseString()
            {
                var sb=new StringBuilder(); r.Read();
                while(true){int c=r.Read(); if(c=='"')break; sb.Append((char)c);}
                return sb.ToString();
            }
            double ParseNumber()
            {
                var sb=new StringBuilder();
                while(true){int c=r.Peek(); if(c==-1||" ,]}".IndexOf((char)c)>=0)break; sb.Append((char)r.Read());}
                return double.Parse(sb.ToString(),System.Globalization.CultureInfo.InvariantCulture);
            }
            void Eat(){while(char.IsWhiteSpace((char)r.Peek()))r.Read();}
        }
    }
}
