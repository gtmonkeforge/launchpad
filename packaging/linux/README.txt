MonkeForge Launchpad
====================

Launchpad is the desktop companion and mod manager for MonkeForge. See https://monkeforge.org/software/launchpad.

Requirements
------------
- x86-64 Linux
- WebKitGTK 4.1 and libnotify
    Debian/Ubuntu: sudo apt install libwebkit2gtk-4.1-0 libnotify4
    Fedora:        sudo dnf install webkit2gtk4.1 libnotify
    Arch:          sudo pacman -S webkit2gtk-4.1 libnotify

Install
-------
    ./install.sh

This copies Launchpad to ~/.local/share/monkeforge-launchpad and adds it to your applications menu. You can also run ./Launchpad straight from this folder without installing.

Uninstall
---------
    ~/.local/share/monkeforge-launchpad/uninstall.sh

Your settings, sign-in and installed mods are kept in ~/.config/launchpad and your game folder.
