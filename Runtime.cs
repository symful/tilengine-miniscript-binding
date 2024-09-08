using Miniscript;
using Tilengine;

class Runtime
{
    readonly Interpreter interpreter;
    Engine engine;
    Window window;


    Runtime(Interpreter interpreter)
    {
        this.interpreter = interpreter;

        Inject();
    }

    static void Print(string s, bool lineBreak = true)
    {
        if (lineBreak) Console.WriteLine(s);
        else Console.Write(s);
    }

    static void ListErrors(Script script)
    {
        if (script.errors == null)
        {
            Print("No errors.");
            return;
        }
        foreach (Miniscript.Error err in script.errors)
        {
            Print(string.Format("{0} on line {1}: {2}",
                err.type, err.lineNum, err.description));
        }

    }

    static void Test(List<string> sourceLines, int sourceLineNum,
                     List<string> expectedOutput, int outputLineNum)
    {
        expectedOutput ??= [];
        //		Console.WriteLine("TEST (LINE {0}):", sourceLineNum);
        //		Console.WriteLine(string.Join("\n", sourceLines));
        //		Console.WriteLine("EXPECTING (LINE {0}):", outputLineNum);
        //		Console.WriteLine(string.Join("\n", expectedOutput));

        Interpreter miniscript = new(sourceLines);
        List<string> actualOutput = [];
        miniscript.standardOutput = (string s, bool eol) => actualOutput.Add(s);
        miniscript.errorOutput = miniscript.standardOutput;
        miniscript.implicitOutput = (s, eol) => { };
        miniscript.RunUntilDone(60, false);

        //		Console.WriteLine("ACTUAL OUTPUT:");
        //		Console.WriteLine(string.Join("\n", actualOutput));

        int minLen = expectedOutput.Count < actualOutput.Count ? expectedOutput.Count : actualOutput.Count;
        for (int i = 0; i < minLen; i++)
        {
            if (actualOutput[i] != expectedOutput[i])
            {
                Print(string.Format("TEST FAILED AT LINE {0}\n  EXPECTED: {1}\n    ACTUAL: {2}",
                    outputLineNum + i, expectedOutput[i], actualOutput[i]));
            }
        }
        if (expectedOutput.Count > actualOutput.Count)
        {
            Print(string.Format("TEST FAILED: MISSING OUTPUT AT LINE {0}", outputLineNum + actualOutput.Count));
            for (int i = actualOutput.Count; i < expectedOutput.Count; i++)
            {
                Print("  MISSING: " + expectedOutput[i]);
            }
        }
        else if (actualOutput.Count > expectedOutput.Count)
        {
            Print(string.Format("TEST FAILED: EXTRA OUTPUT AT LINE {0}", outputLineNum + expectedOutput.Count));
            for (int i = expectedOutput.Count; i < actualOutput.Count; i++)
            {
                Print("  EXTRA: " + actualOutput[i]);
            }
        }

    }

    static void RunTestSuite(string path)
    {
        StreamReader file = new StreamReader(path);
        if (file == null)
        {
            Print("Unable to read: " + path);
            return;
        }

        List<string>? sourceLines = null;
        List<string>? expectedOutput = null;
        int testLineNum = 0;
        int outputLineNum = 0;

        string? line = file.ReadLine();
        int lineNum = 1;
        while (line != null)
        {
            if (line.StartsWith("===="))
            {
                if (sourceLines != null) Test(sourceLines, testLineNum, expectedOutput!, outputLineNum);
                sourceLines = null;
                expectedOutput = null;
            }
            else if (line.StartsWith("----"))
            {
                expectedOutput = [];
                outputLineNum = lineNum + 1;
            }
            else if (expectedOutput != null)
            {
                expectedOutput.Add(line);
            }
            else
            {
                if (sourceLines == null)
                {
                    sourceLines = [];
                    testLineNum = lineNum;
                }
                sourceLines.Add(line);
            }

            line = file.ReadLine();
            lineNum++;
        }
        if (sourceLines != null) Test(sourceLines, testLineNum, expectedOutput!, outputLineNum);
        Print("\nIntegration tests complete.\n");
    }
    public static void RunFile(string path, bool dumpTAC = false)
    {
        StreamReader file = new(path);
        if (file == null)
        {
            Print("Unable to read: " + path);
            return;
        }

        List<string> sourceLines = [];
        while (!file.EndOfStream) sourceLines.Add(file.ReadLine()!);

        Interpreter miniscript = new(sourceLines)
        {
            standardOutput = Print
        };
        miniscript.Compile();

        if (dumpTAC && miniscript.vm != null)
        {
            miniscript.vm.DumpTopContext();
        }

        Runtime runtime = new(miniscript);

        runtime.Run();
    }

    void Run()
    {
        Update();

        while (window.Process())
        {
            Update();
        }

    }

    void Update()
    {
        if (interpreter.Running())
        {
            interpreter.RunUntilDone(int.MaxValue, false);
        }
        Invoke((ValFunction)interpreter.GetGlobalValue("loop"), []);
    }

    ValFunction GetSequencePackFind(SequencePack sequencePack)
    {
        Intrinsic f = Intrinsic.Create("find");

        f.AddParam("name");
        f.code = (context, partialResult) =>
        {
            return new Intrinsic.Result(GetSequence(sequencePack.Find(context.GetLocalString("name"))));
        };

        return f.GetFunc();
    }

    ValFunction GetSequencePackFromFile()
    {
        Intrinsic f = Intrinsic.Create("fromFile");

        f.AddParam("path");
        f.code = (context, partialResult) =>
        {
            SequencePack map = SequencePack.FromFile(context.GetLocalString("path"));
            ValMap m = new();

            m.SetElem(new ValString("find"), GetSequencePackFind(map));
            //Print("Injected <SequencePack>.find");

            return new Intrinsic.Result(m);
        };

        return f.GetFunc();
    }

    ValMap GetSequencePack()
    {
        ValMap m = new();

        m.SetElem(new ValString("fromFile"), GetSequencePackFromFile());

        return m;
    }

    ValFunction GetEngineGetInput()
    {
        Intrinsic f = Intrinsic.Create("getInput");

        f.AddParam("id");
        f.code = (context, partialResult) =>
        {
            Input input = (Input)context.GetLocalInt("id");

            return window.GetInput(input) ? Intrinsic.Result.True : Intrinsic.Result.False;
        };

        return f.GetFunc();
    }

    ValFunction GetTilemapSetLayer(Tilemap map)
    {
        Intrinsic f;

        {
            f = Intrinsic.Create("setLayer");

            f.AddParam("index");
            f.code = (context, partialResult) =>
            {
                int idx = context.GetLocalInt("index");

                engine.Layers[idx].SetMap(map);

                return Intrinsic.Result.Null;
            };
        }

        return f.GetFunc();
    }

    ValFunction GetTilemapFromFile()
    {
        Intrinsic f = Intrinsic.Create("fromFile");

        f.AddParam("path");
        f.code = (context, partialResult) =>
        {
            Tilemap map = Tilemap.FromFile(context.GetLocalString("path"), null!);
            ValMap m = new();

            m.SetElem(new ValString("setLayer"), GetTilemapSetLayer(map));
            //Print("Injected <Tilemap>.setLayer");

            return new Intrinsic.Result(m);
        };

        return f.GetFunc();
    }


    ValMap GetTilemap()
    {
        ValMap m = new();


        m.SetElem(new ValString("fromFile"), GetTilemapFromFile());
        //Print("Injected <Engine>.Tilemap.fromFile");

        return m;
    }
    ValFunction GetEngineDrawFrame()
    {
        Intrinsic f = Intrinsic.Create("drawFrame");

        f.AddParam("frame");
        f.code = (context, partialResult) =>
        {
            window.DrawFrame(context.GetLocalInt("frame"));

            return Intrinsic.Result.Null;
        };

        return f.GetFunc();
    }

    VideoCallback keepAlive;

    ValFunction GetEngineSetRasterCallback()
    {
        Intrinsic f = Intrinsic.Create("setRasterCallback");

        f.AddParam("callback");
        f.code = (context, partialResult) =>
        {
            keepAlive = (int line) =>
            {
                Invoke((ValFunction)context.GetLocal("callback"), [new ValNumber(line)]);
            };

            engine.SetRasterCallback(keepAlive);

            return Intrinsic.Result.Null;
        };

        return f.GetFunc();
    }

    ValMap GetEngine(int hres, int vres, int numLayers, int numSprites, int numAnimations, WindowFlags flag, string loadPath)
    {
        engine = Engine.Init(hres, vres, numLayers, numSprites, numAnimations);
        window = Window.Create(null!, flag);

        engine.LoadPath = loadPath;

        ValMap r = new();

        r.SetElem(new ValString("SequencePack"), GetSequencePack());
        //Print("Injected <SequencePack>");

        r.SetElem(new ValString("Tilemap"), GetTilemap());
        //Print("Injected <Engine>.Tilemap");

        r.SetElem(new ValString("drawFrame"), GetEngineDrawFrame());
        //Print("Injected <Engine>.drawFrame");

        r.SetElem(new ValString("setRasterCallback"), GetEngineSetRasterCallback());
        //Print("Injected <Engine>.setRasterCallback");

        r.SetElem(new ValString("getInput"), GetEngineGetInput());
        //Print("Injected <Engine>.getInput");



        r.SetElem(new ValString("setLayerPosition"), GetEngineSetLayerPosition());
        //Print("Injected <Engine>.setLayerPosition");

        r.SetElem(new ValString("setBackgroundColor"), GetEngineSetBackgroundColor());
        //Print("Injected <Engine>.setBackgroundColor");

        return r;
    }

    ValFunction GetTilengineInit()
    {
        Intrinsic f = Intrinsic.Create("init");
        f.AddParam("hres");
        f.AddParam("vres");
        f.AddParam("numLayers");
        f.AddParam("numSprites");
        f.AddParam("numAnimations");
        f.AddParam("flag");
        f.AddParam("loadPath");
        f.code = (context, partialResult) =>
        {
            return new Intrinsic.Result(GetEngine(context.GetLocalInt("hres"), context.GetLocalInt("vres"), context.GetLocalInt("numLayers"), context.GetLocalInt("numSprites"), context.GetLocalInt("numAnimations"), (WindowFlags)context.GetLocalInt("flag"), context.GetLocalString("loadPath")));
        };

        return f.GetFunc();
    }

    ValFunction GetEngineSetLayerPosition()
    {
        Intrinsic f = Intrinsic.Create("setLayerPosition");
        f.AddParam("index");
        f.AddParam("x");
        f.AddParam("y");
        f.code = (context, partialResult) =>
        {
            engine.Layers[context.GetLocalInt("index")].SetPosition(context.GetLocalInt("x"), context.GetLocalInt("y"));

            return Intrinsic.Result.Null;
        };

        return f.GetFunc();
    }

    ValFunction GetEngineSetBackgroundColor()
    {
        Intrinsic f = Intrinsic.Create("setBackgroundColor");
        f.AddParam("r");
        f.AddParam("g");
        f.AddParam("b");
        f.code = (context, partialResult) =>
        {
            engine.SetBackgroundColor(new Color((byte)context.GetLocalInt("r"), (byte)context.GetLocalInt("g"), (byte)context.GetLocalInt("b")));

            return Intrinsic.Result.Null;
        };

        return f.GetFunc();
    }

    ValFunction GetSequenceSetPaletteAnimation(Sequence sequence)
    {
        Intrinsic f = Intrinsic.Create("setPaletteAnimation");
        f.AddParam("index");
        f.AddParam("layerIndex");
        f.AddParam("blend");
        f.code = (context, partialResult) =>
        {
            engine.Animations[context.GetLocalInt("index")].SetPaletteAnimation(engine.Layers[context.GetLocalInt("layerIndex")].Palette, sequence, context.GetLocalBool("blend"));

            return Intrinsic.Result.Null;
        };

        return f.GetFunc();
    }

    ValMap GetSequence(Sequence sequence)
    {
        ValMap m = new();

        m.SetElem(new ValString("setPaletteAnimation"), GetSequenceSetPaletteAnimation(sequence));
        //Print("Injected <Sequence>.setPaletteAnimation");

        return m;
    }



    /*ValFunction GetTest()
    {

        Intrinsic f = Intrinsic.Create("test");
        f.AddParam("callback");
        f.code = (context, partialResult) =>
        {
            ValFunction f = (ValFunction)context.GetLocal("callback");
            Value[] l = [new ValString("Halo Dunia!")];

            Value d = Invoke(f, l);

            Print("Result: " + d.ToString());

            return Intrinsic.Result.Null;
        };

        return f.GetFunc();
    }*/

    //delegate void Callback(Value value);

    void Invoke(ValFunction function, Value[] arguments)
    {
        interpreter.vm.ManuallyPushCall(function, null, [.. arguments]);
    }
    /*void InvokeValue(ValFunction function, Value[] arguments)
    {
        interpreter.vm.ManuallyPushCall(function, null, [.. arguments]);
    }*/
    ValMap GetTilengine()
    {
        ValMap tilengine = new();

        tilengine.SetElem(new ValString("init"), GetTilengineInit());
        //Print("Injected Tilengine.init");

        tilengine.SetElem(new ValString("flags"), GetFlags());
        //Print("Injected Tilengine.flags");

        //tilengine.SetElem(new ValString("test"), GetTest());
        //Print("Injected Tilengine.test");

        tilengine.SetElem(new ValString("inputs"), GetInputs());
        //Print("Injected Tilengine.inputs");


        return tilengine;
    }

    static ValMap GetFlags()
    {
        ValMap m = new();

        m.SetElem(new ValString("fullscreen"), new ValNumber((double)WindowFlags.Fullscreen));
        m.SetElem(new ValString("nearest"), new ValNumber((double)WindowFlags.Nearest));
        m.SetElem(new ValString("s1"), new ValNumber((double)WindowFlags.S1));
        m.SetElem(new ValString("s2"), new ValNumber((double)WindowFlags.S2));
        m.SetElem(new ValString("s3"), new ValNumber((double)WindowFlags.S3));
        m.SetElem(new ValString("s4"), new ValNumber((double)WindowFlags.S4));
        m.SetElem(new ValString("s5"), new ValNumber((double)WindowFlags.S5));
        m.SetElem(new ValString("vsync"), new ValNumber((double)WindowFlags.Vsync));

        return m;
    }

    static ValMap GetInputs()
    {
        ValMap m = new();

        m.SetElem(new ValString("button1"), new ValNumber((double)Input.Button1));
        m.SetElem(new ValString("button2"), new ValNumber((double)Input.Button2));
        m.SetElem(new ValString("button3"), new ValNumber((double)Input.Button3));
        m.SetElem(new ValString("button4"), new ValNumber((double)Input.Button4));
        m.SetElem(new ValString("button5"), new ValNumber((double)Input.Button5));
        m.SetElem(new ValString("button6"), new ValNumber((double)Input.Button6));
        m.SetElem(new ValString("buttonA"), new ValNumber((double)Input.Button_A));
        m.SetElem(new ValString("buttonB"), new ValNumber((double)Input.Button_B));
        m.SetElem(new ValString("buttonC"), new ValNumber((double)Input.Button_C));
        m.SetElem(new ValString("buttonD"), new ValNumber((double)Input.Button_D));
        m.SetElem(new ValString("buttonE"), new ValNumber((double)Input.Button_E));
        m.SetElem(new ValString("buttonF"), new ValNumber((double)Input.Button_F));
        m.SetElem(new ValString("down"), new ValNumber((double)Input.Down));
        m.SetElem(new ValString("left"), new ValNumber((double)Input.Left));
        m.SetElem(new ValString("right"), new ValNumber((double)Input.Right));
        m.SetElem(new ValString("up"), new ValNumber((double)Input.Up));
        m.SetElem(new ValString("none"), new ValNumber((double)Input.None));
        m.SetElem(new ValString("p1"), new ValNumber((double)Input.P1));
        m.SetElem(new ValString("p2"), new ValNumber((double)Input.P2));
        m.SetElem(new ValString("p3"), new ValNumber((double)Input.P3));
        m.SetElem(new ValString("p4"), new ValNumber((double)Input.P4));
        m.SetElem(new ValString("start"), new ValNumber((double)Input.Start));

        return m;
    }

    void Inject()
    {
        interpreter.SetGlobalValue("Tilengine", GetTilengine());
        //Print("Injected Tilengine");
    }

}