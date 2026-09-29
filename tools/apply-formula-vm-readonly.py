from pathlib import Path
p=Path('src/ProDataGrid.FormulaEngine/FormulaEvaluation.cs')
s=p.read_text(encoding='utf-8')
old='''                foreach (var instruction in instructions)
                {
                    switch (instruction.Kind)'''
new='''                for (var instructionIndex = 0; instructionIndex < instructions.Length; instructionIndex++)
                {
                    // Instructions include large reference descriptors. Read the immutable entry
                    // in place instead of copying its entire struct at every VM step.
                    ref readonly var instruction = ref instructions[instructionIndex];
                    switch (instruction.Kind)'''
assert s.count(old)==1
p.write_text(s.replace(old,new),encoding='utf-8')
