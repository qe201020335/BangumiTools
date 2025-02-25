#!/usr/bin/env bash
# echo "Hello, World!"
set -e
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
Font7z=`find . -maxdepth 1 -name "*Fonts*.7z" -print -quit` # -quit to only find the first one

if [ -f "$Font7z" ]; then
    echo "Extracting Fonts: $Font7z"
    7za x "$Font7z" -o"./fonts" -y -bso0
    echo "fonts: "
    ls ./fonts
    mv "$Font7z" "$Extra"
else
    echo "Fonts 7z not found."
    # TODO check existing fonts and zip it
fi

# process subs
zip "${Extra}subs.zip" ./*.ass
ls -lh "${Extra}subs.zip"

# find tc subs
# ^(?i).*\.(((jpn?|jap?).?)?(tc|cht)(.?(jpn?|jap?))?)\.ass$
# ^(?i).*\.([A-Za-z0-9_&]*(tc|cht)[A-Za-z0-9_&]*)\.ass$
TcSubs=`find . -maxdepth 1 -name "*.ass" -type f | grep -P '^(?i).*\.([A-Za-z0-9_&]*(tc|cht|big5)[A-Za-z0-9_&]*)\.ass$' | xargs --no-run-if-empty -d '\n' -n 1 basename | sort`
echo "$TcSubs"
if [[ $TcSubs != '' ]]; then
    read -p "Delete above tc subs? <y/N> " prompt
    if [[ $prompt =~ [yY](es)* ]]; then
        # TODO
        echo "rm"
        echo "$TcSubs" | xargs -d '\n' rm
    else
        echo "Abort"
        exit 1
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

