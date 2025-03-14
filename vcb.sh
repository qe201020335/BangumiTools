#!/usr/bin/env bash
set -e
set -u
set -o pipefail
# set -x

Extra="./Extra/"
mkdir -p $Extra

optMoveToExtra() {
    if [ -d "$1" ]; then
        echo "Moving $1 to Extra"
        mv "$1" "$Extra" 
    else
        echo "Skip moving $1"
    fi
}

optMoveToExtra ./CDs
optMoveToExtra ./Scans
optMoveToExtra ./SPs


# process fonts
Font7z=`find . -maxdepth 1 -iname "*fonts*.7z" -print -quit` # -quit to only find the first one
FontRar=`find . -maxdepth 1 -iname "*fonts*.rar" -print -quit`
FontZip=`find . -maxdepth 1 -iname "*fonts*.zip" -print -quit`

if [ -f "$Font7z" ]; then
    echo "Extracting Fonts: $Font7z"
    7za x "$Font7z" -o"./fonts" -y -bso0
    echo "fonts: "
    ls ./fonts
    mv "$Font7z" "$Extra"
elif [ -f "$FontRar" ]; then
    echo "Extracting Fonts: $FontRar"
    unrar e -op./fonts -- "$FontRar"
    echo "fonts: "
    ls ./fonts
    mv "$FontRar" "$Extra"
elif [ -f "$FontZip" ]; then
    echo "Extracting Fonts: $FontZip"
    unzip -j "$FontZip" -d ./fonts
    echo "fonts: "
    ls ./fonts
    mv "$FontZip" "$Extra"
else
    echo "Fonts 7z/rar/zip not found."
    # check existing fonts and zip it
    if [ -d "./fonts" ]; then
        echo "Zipping fonts from ./fonts"
        7za a "${Extra}Fonts.7z" ./fonts/*
        ls -lh "${Extra}Fonts.7z"
    elif [ -d "./Fonts" ]; then
        echo "Zipping fonts from ./Fonts"
        7za a "${Extra}Fonts.7z" ./Fonts/*
        ls -lh "${Extra}Fonts.7z"
    else
        echo "No fonts found."
    fi
fi

# process subs
zip "${Extra}subs.zip" ./*.ass
ls -lh "${Extra}subs.zip"

# find tc subs
# ^(?i).*\.(((jpn?|jap?).?)?(tc|cht)(.?(jpn?|jap?))?)\.ass$
# ^(?i).*\.([A-Za-z0-9_&]*(tc|cht)[A-Za-z0-9_&]*)\.ass$
TcSubs=$(find . -maxdepth 1 -name "*.ass" -type f | { grep -P '^(?i).*\.([A-Za-z0-9_&\-]*(tc|cht|big5)[A-Za-z0-9_&\-]*)\.ass$' || test $? = 1; } | xargs --no-run-if-empty -d '\n' -n 1 basename | sort)
echo "$TcSubs"
if [[ $TcSubs != '' ]]; then
    read -p "Delete above tc subs? <y/N> " prompt
    if [[ $prompt =~ [yY](es)* ]]; then
        echo "rm"
        echo "$TcSubs" | xargs -d '\n' rm
    else
        echo "not deleting"
    fi
fi

echo "files:"
ls -hl

read -p "Do merge? <y/N> " prompt
if [[ $prompt =~ [yY](es)* ]]; then
    # TODO
    echo "let's merge"
    BangumiMerge ./*.mkv
fi

