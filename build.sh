dotnet publish -c Release --sc true -r win-x64 -p:PublishSingleFile=true -o output /p:DebugType=None /p:DebugSymbols=false
zip Tarot-Win64 output/Tarot.exe output/e_sqlite3.dll output/DeckDB.db
rm output/*
dotnet publish -c Release --sc true -r osx-x64 -p:PublishSingleFile=true -o output /p:DebugType=None /p:DebugSymbols=false  
zip Tarot-Osx64 output/Tarot output/libe_sqlite3.dylib output/DeckDB.db
rm output/*
dotnet publish -c Release --sc true -r linux-x64 -p:PublishSingleFile=true -o output /p:DebugType=None /p:DebugSymbols=false
zip Tarot-Linux64 output/Tarot output/libe_sqlite3.so output/DeckDB.db
rm output/*
mv *.zip output/
