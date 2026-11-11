#!/usr/bin/env bash
dotnet publish -c Release -o ./dist 
ModFolder="$HOME/.config/r2modmanPlus-local/REPO/profiles/MEOW/BepInEx/plugins/Catburger-WorthALittle/"
cp ./dist/WorthALittle.dll "$ModFolder"
# cp ./UnityProject/Assets/AssetBundles/skeeg $ModFolder
exit