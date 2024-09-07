using Miniscript;
using Tilengine;

class Program
{
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
		if (expectedOutput == null) expectedOutput = [];
		//		Console.WriteLine("TEST (LINE {0}):", sourceLineNum);
		//		Console.WriteLine(string.Join("\n", sourceLines));
		//		Console.WriteLine("EXPECTING (LINE {0}):", outputLineNum);
		//		Console.WriteLine(string.Join("\n", expectedOutput));

		Interpreter miniscript = new(sourceLines);
		List<string> actualOutput = [];
		miniscript.standardOutput = (string s, bool eol) => actualOutput.Add(s);
		miniscript.errorOutput = miniscript.standardOutput;
		miniscript.implicitOutput = miniscript.standardOutput;
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
	static void RunFile(string path, bool dumpTAC = false)
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
		miniscript.implicitOutput = miniscript.standardOutput;
		miniscript.Compile();

		if (dumpTAC && miniscript.vm != null)
		{
			miniscript.vm.DumpTopContext();
		}

		Inject(miniscript);

		while (!miniscript.done)
		{
			miniscript.RunUntilDone(int.MaxValue, false);
		}
	}

	static void Inject(Interpreter interpreter)
	{
		ValMap tilengine = new();
		ValMap m;
		Engine engine;
		Window window;

		Intrinsic f;

		{
			f = Intrinsic.Create("init");
			f.AddParam("hres");
			f.AddParam("vres");
			f.AddParam("numLayers");
			f.AddParam("numSprites");
			f.AddParam("numAnimations");
			f.AddParam("flag");
			f.AddParam("loadPath");
			f.code = (context, partialResult) =>
			{
				engine = Engine.Init(context.GetLocalInt("hres"), context.GetLocalInt("vres"), context.GetLocalInt("numLayers"), context.GetLocalInt("numSprites"), context.GetLocalInt("numAnimations"));
				window = Window.Create(null!, (WindowFlags)context.GetLocalInt("flag"));

				engine.LoadPath = context.GetLocalString("loadPath");

				ValMap r = new();
				ValMap m;
				Intrinsic f;

				{
					m = new();
					f = Intrinsic.Create("fromFile");

					f.AddParam("path");
					f.code = (context, partialResult) =>
					{
						Tilemap map = Tilemap.FromFile(context.GetLocalString("path"), null!);
						ValMap r = new();
						ValMap m;
						Intrinsic f;

						m = new();

						{
							f = Intrinsic.Create("setLayer");

							f.AddParam("index");
							f.code = (context, partialResult) =>
							{
								int idx = context.GetLocalInt("index");

								engine.Layers[idx].SetMap(map);

								return Intrinsic.Result.Null;
							};

							m.SetElem(new ValString(f.name), f.GetFunc());
							//Print("Injected <Engine>.<Tilemap>.setLayer");
						}

						return new Intrinsic.Result(m);
					};

					//Print("Injected <Engine>.Tilemap.fromFile");
					m.SetElem(new ValString(f.name), f.GetFunc());

					//Print("Injected <Engine>.Tilemap");
					r.SetElem(new ValString("Tilemap"), m);
				}

				{
					f = Intrinsic.Create("process");

					f.code = (context, partialResult) =>
					{
						bool r = window.Process();

						return r ? Intrinsic.Result.True : Intrinsic.Result.False;
					};
					r.SetElem(new ValString(f.name), f.GetFunc());

					//Print("Injected <Engine>.process");
				}

				{
					f = Intrinsic.Create("drawFrame");

					f.AddParam("frame");
					f.code = (context, partialResult) =>
					{
						window.DrawFrame(context.GetLocalInt("frame"));

						return Intrinsic.Result.Null;
					};
					r.SetElem(new ValString(f.name), f.GetFunc());
					//Print("Injected <Engine>.drawFrame");
				}

				return new Intrinsic.Result(r);
			};
			tilengine.SetElem(new ValString(f.name), f.GetFunc());

			//Print("Injected Tilengine.init");
		}


		{
			m = new();

			m.SetElem(new ValString("fullscreen"), new ValNumber((double)WindowFlags.Fullscreen));
			m.SetElem(new ValString("nearest"), new ValNumber((double)WindowFlags.Nearest));
			m.SetElem(new ValString("s1"), new ValNumber((double)WindowFlags.S1));
			m.SetElem(new ValString("s2"), new ValNumber((double)WindowFlags.S2));
			m.SetElem(new ValString("s3"), new ValNumber((double)WindowFlags.S3));
			m.SetElem(new ValString("s4"), new ValNumber((double)WindowFlags.S4));
			m.SetElem(new ValString("s5"), new ValNumber((double)WindowFlags.S5));
			m.SetElem(new ValString("vsync"), new ValNumber((double)WindowFlags.Vsync));
			tilengine.SetElem(new ValString("flags"), m);

			//Print("Injected Tilengine.flags");
		}


		//Print("Injected Tilengine");

		interpreter.SetGlobalValue("Tilengine", tilengine);
	}

	static int Main(string[] args)
	{
		RunFile(args[0]);

		return 0;
	}
}