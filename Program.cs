using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VMtranslator
{
    class Program
    {
        static void Main(string[] args)
        {
            List<String> Files = new List<string>();
            string file = "C:\\Users\\Matthew\\Downloads\\nand2tetris\\projects\\08\\FunctionCalls\\SimpleFunction";
            if (args.Length > 0)
                file = args[0];

            if(file.Contains(".vm")) {
                Files.Add(file);
            } else {
                try
                {
                    string[] fs = Directory.GetFiles(file, "*.vm");
                    foreach (string f in fs) Files.Add(f);
                }
                catch (Exception e)
                {
                    Console.WriteLine("Invalid Directory.");
                }
            }


            CodeWriter cw = new CodeWriter(Files[0]);

            foreach (string f in Files)
            {
                Parser p = new Parser(f);
                cw.setFileName(Path.GetFileName(f));
                while (p.hasMoreCommands())
                {
                    p.advance();
                    if (p.ToString() == "") continue;
                    cw.writeCommand(p.currentCommand, p.type, p.arg1, p.arg2i);
                }
            }
            cw.Close();
        }
        class Parser
        {
            string[] lines;
            int index = 0;
            public string currentCommand, arg1, arg2, type;
            public int arg2i=0;
            public Parser(string input)
            {
                lines = File.ReadAllLines(input);
            }
            public bool hasMoreCommands()
            {
                return index < lines.Length-1;
            }
            public void advance()
            {
                currentCommand = arg1 = arg2 = "";
                arg2i = 0;
                index++;
                string line = lines[index];
                if (line.Contains(@"//"))
                {
                    line = line.Substring(0, line.IndexOf(@"//"));
                }
                if (line == "") return;
                string[] args = line.Split(' ');
                currentCommand = args.Length > 0 ? args[0] : "";
                switch (currentCommand.ToLower())
                {
                    case "push":
                    case "pop":
                    case "label":
                    case "goto":
                    case "if":
                    case "function":
                    case "return":
                    case "call":
                        type = "C_"+currentCommand.ToUpper();
                        break;
                    case "add":
                    case "sub":
                    case "neg":
                    case "eq": 
                    case "gt": 
                    case "lt": 
                    case "and":
                    case "or":
                    case "not":
                        type = "C_ARITHMETIC";
                        break;
                    default:
                        type = "";
                        return;
                }
                arg1 = args.Length > 1 ? args[1] : "";
                arg2 = args.Length > 2 ? args[2] : "";
                int.TryParse(arg2, out arg2i);
            }
            override public string ToString()
            {
                if (currentCommand == "") return "";
                return "[" + lines[index] + "] => [" + currentCommand + "("+arg1+", "+arg2+")" +"]";
            }
        }
        class CodeWriter
        {
            System.IO.StreamWriter output;
            string fileName;
            public CodeWriter(string file)
            {
                setFileName(file);
                output = new System.IO.StreamWriter(file.Replace(".vm", "")+".asm", false);
                writeInit();
            }
            public void setFileName(string file)
            {
                fileName = file;
            }
            Dictionary<string, int> regBase = new Dictionary<string, int> { {"LCL", 1}, {"ARG", 2}, {"THIS", 3}, {"THAT", 4}, { "reg", 1 }, { "pointer", 3 }, { "temp", 5 } };
            Dictionary<string, string> memName = new Dictionary<string, string> { { "local", "LCL" }, { "argument", "ARG" }, { "this", "THIS" }, { "that", "THAT" } };
            int labelNum = 0;

            

            public void writeCommand(string command, string type, string segment, int index)
            {
                //output.WriteLine("");
                switch(type) {
                    case "C_ARITHMETIC":
                        writeArithmetic(command);
                        break;
                    case "C_PUSH":
                    case "C_POP":
                        pushPop(command, segment, index.ToString());
                        break;
                    case "C_LABEL":
                        writeLabel(segment);
                        break;
                    case "C_GOTO":
                        writeGoto(segment);
                        break;
                    case "C_IF":
                        writeIf(segment);
                        break;
                    case "C_FUNCTION":
                        writeFunction(segment, index);
                        break;
                    case "C_RETURN":
                        writeReturn();
                        break;
                    case "C_CALL":
                        writeCall(segment,index);
                        break;
                }
            }
            
            public void Close() {
                output.Close();
            }

            // Write
            public void writeInit() { comment("Begin Init"); a("256"); c("D", "A"); compToReg("0", "D"); writeCall("Sys.init", 0); comment("End Init"); }
            void writeArithmetic(string command)
            {
                comment("Arithmetic:");
                switch (command)
                {
                    case "add": binary("D+A"); break;
                    case "sub": binary("A-D"); break;
                    case "neg": unary("-D"); break;
                    case "eq": compare("JEQ"); break;
                    case "gt": compare("JGT"); break;
                    case "lt": compare("JLT"); break;
                    case "and": binary("D&A"); break;
                    case "or": binary("D|A"); break;
                    case "not": unary("!D"); break;
                }
            }
            void writeLabel(string label) { l(label); }
            void writeIf(string label) { comment("If:"); popToDest("D"); a(label); c("", "D", "JNE"); }
            void copyRegisterToStack(string reg)
            {
                comment("CopyRegToStack( "+reg+" ): " );
                a(reg);
                cDM();
                loadSp();
                cMD();
                incSp();
            }
            void writeCall(string name, int numArgs)
            {
                comment("WriteCall - " + name + "( "+numArgs+ "args ):" );
                string returnAddress = newLabel();
                pushPop("push", "constant", returnAddress);
                copyRegisterToStack("LCL");
                copyRegisterToStack("ARG");
                copyRegisterToStack("THIS");
                copyRegisterToStack("THAT");

                //ARG = SP - n - 5

                a("SP");
                cDM();
                a(numArgs.ToString());
                c("D", "D-A");
                a("5");
                c("D", "D-A");
                a("ARG");
                cMD();

                //LCL = SP
                a("SP");
                cDM();
                a("LCL");
                cMD();


                a(name);
                c("", "0", "JMP");

                l(returnAddress);
            }
            void writeGoto(string label) { a(label); c("", "0", "JMP"); }
            void writeReturn()
            {
                regToReg("13", "1");
                a("S");
                c("A", "D-A");
                cDM();
                compToReg("14", "D");
                pushPop("pop", "argument", "0");
                regToDest("D", "2");
                compToReg("0", "D+1");
                prevFrameToReg("1");
                prevFrameToReg("2");
                prevFrameToReg("3");
                prevFrameToReg("4");
                regToDest("A", "14");
                c("", "0", "JMP");
            }
            void writeFunction(string name, int locals)
            {
                l(name);
                for(int i = 0; i < locals; i++)
                    pushPop("push", "constant", "0");
            }

            void pushPop(string command, string seg, string index)
            {
                if (command == "push")
                {
                    if (seg == "constant") valToStack(index);
                    else if (memName.ContainsKey(seg)) memToStack(memName[seg], int.Parse(index));
                    else if (seg == "reg" || seg == "temp" || seg == "pointer") regToStack(seg, int.Parse(index));
                    else if (seg == "static") staticToStack(seg, int.Parse(index));
                    incSp();
                }
                else
                {
                    decSp();
                    if (memName.ContainsKey(seg)) stackToMem(memName[seg], int.Parse(index));
                    else if (seg == "reg" || seg == "temp" || seg == "pointer") stackToReg(seg, int.Parse(index));
                    else if (seg == "static") stackToStatic(seg, int.Parse(index));
                }
            }

            void popToDest(string dest) { decSp(); stackToDest(dest); }

            void prevFrameToReg(string reg)
            {
                comment("prevFrameToReg", 1);
                regToDest("D", "13");
                c("D", "D-1");
                compToReg("13", "D");
                c("A", "D");
                c("D", "M");
                compToReg(reg, "D");
                comment("prevFrameToReg", 2);
            }
            
            //Arithmatic
            void unary(string comp) { comment("Unary:" + comp, 1); decSp(); stackToDest("D"); c("D", comp); compToStack(); incSp(); comment("Unary:" + comp, 2); }
            void binary(string comp) { comment("Binary:" + comp, 1); decSp(); stackToDest("D"); decSp(); stackToDest("A"); c("D", comp); compToStack(); incSp(); comment("Binary:" + comp, 2); }
            void compare(string jump) 
            {
                comment("Compare:"+jump, 1);
                decSp();
                stackToDest("D");
                decSp();
                stackToDest("A");
                c("D", "A-D");
                string labeq = jumpToNewLabel("D", jump);
                compToStack("0");
                string labne = jumpToNewLabel("0", "JMP");
                l(labeq);
                compToStack("-1");
                l(labne);
                incSp();
                comment("Compare:" + jump, 2);
            }
            
            // SP
            void incSp() { comment("IncSp", 1); a("SP"); c("M", "M+1"); comment("IncSp", 2); }
            void decSp() { comment("DecSp", 1); a("SP"); c("M", "M-1"); comment("DecSp", 2); }
            void loadSp() { comment("LoadSp", 1); a("SP"); c("A", "M"); comment("LoadSp", 2); }

            // Store onto stack
            void valToStack(string val) { a(val); c("D", "A"); compToStack(); }
            void regToStack(string seg, int index) { regToDest("D", regNum(seg, index)); compToStack(); }
            void memToStack(string seg, int index, bool ind = true) { comment("memToStack:" + seg + " to " + index, 1); loadSeg(seg, index, ind); cDM(); compToStack(); comment("memToStack:" + seg + " to " + index, 2); }
            void staticToStack(string seg, int index) { a(fileName + "." + index); cDM(); compToStack(); }
            void compToStack(string comp="D") { loadSp(); c("M", comp); }

            // Retrieve from stack
            string regNum(string seg, int index) { return "R" + (regBase[seg] + index); }
            void stackToReg(string seg, int index) { stackToDest("D"); compToReg(regNum(seg,index), "D"); }
            void stackToMem(string seg, int index, bool ind = true) {
                comment("stackToMem:" + seg + " to " + index, 1);
                loadSeg(seg, index, ind); 
                compToReg("R13", "D"); 
                stackToDest("D"); 
                regToDest("A", "R13"); 
                cMD();
                comment("StackToMem:" + seg + " to " + index, 2);
            }
            void stackToStatic(string seg, int index) { stackToDest("D"); a(fileName + "." + index); cMD(); }
            void stackToDest(string dest) {loadSp(); c(dest, "M");}

            void loadSeg(string seg, int index, bool ind = true)
            {
                if (index == 0)
                {
                    a(seg);
                    if (ind) indir("AD");
                }
                else
                {
                    string comp = "D+A";
                    if (index < 0)
                    {
                        index = -index;
                        comp = "A-D";
                    }
                    a(index.ToString());
                    c("D", "A");
                    a(seg);
                    if (ind) indir();
                    c("AD", comp);
                }
            }

            // Registers
            void regToDest(string dest, string reg) { comment("regToDest:" + reg + " to " + dest, 1); a(reg); c(dest, "M"); }
            void compToReg(string comp, string reg) { comment("compToReg:" + comp + " to " + reg, 1); a(comp); c("M", reg); }
            void regToReg(string dest, string src) { regToDest("D", src); compToReg(dest, "D"); }
            void indir(string dest = "A") { c(dest, "M"); }

            // Labels
            string jumpToNewLabel(string comp, string jump)
            {
                string label = newLabel();
                a(label);
                c("", comp, jump);
                return label;
            }
            string newLabel() { labelNum += 1; return "LABEL" + labelNum; }

            // Commands
            void a(string val) { output.WriteLine("@" + val); }
            void c(string dest, string comp, string jump = "")
            {
                int d;
                if (int.TryParse(dest, out d) && d < 16) dest = "R" + d; 
                string line = "";
                if (dest != "") line += dest + "=";
                line += comp;
                if (jump != "") line += ";" + jump;
                output.WriteLine(line);
            }
            void cDM() { c("D", "M"); }
            void cMD() { c("M", "D"); }
            void l(string label) { output.WriteLine("(" + label + ")"); }
            public bool debug = true;
            void comment(string val, int openClose = 0) { if (!debug) return; output.WriteLine("// " + (openClose == 0 ? val : (openClose == 1 ? ("<" + val + ">") : ("</" + val + ">")))); }

        }
    }
}
