from pathlib import Path
file = Path('tools/formula-benchmarks/Program.cs')
text = file.read_text(encoding='utf-8')
marker = '            Console.Write(output.ToString());'
assert text.count(marker) == 1
text = text.replace(marker, '            ValueStorageBenchmarks.Run(output);\n' + marker)
file.write_text(text, encoding='utf-8')
