echo "Running Tests"

..\..\..\packages\OpenCover.4.6.519\tools\OpenCover.Console.exe -target:"..\..\..\packages\NUnit.ConsoleRunner.3.8.0\tools\nunit3-console.exe" -targetargs:"KCDModMerger.Tests.dll" -filter:"+[KCDModMerger]KCDModMerger* -[KCDModMerger]KCDModMerger.Annotations.*" -excludebyattribute:"System.CodeDom.Compiler.GeneratedCodeAttribute" -register:user -output:"_CodeCoverageResult.xml"

..\..\..\packages\ReportGenerator.3.1.2\tools\ReportGenerator.exe "-reports:_CodeCoverageResult.xml" "-targetdir:_CodeCoverageReport"

pause