﻿using System;
using System.Collections.Generic;
using ImGuiNET;
using UAlbion.Core;
using UAlbion.Core.Events;
using UAlbion.Formats.AssetIds;

namespace UAlbion.Game.Gui
{
    public class MainMenu : Component
    {
        static readonly IList<Handler> Handlers = new Handler[]
        {
            new Handler<MainMenu, EngineUpdateEvent>((x, _) => x._menuFunc()),
            new Handler<MainMenu, SubscribedEvent>((x, _) =>
            {
                var assets = x.Exchange.Resolve<IAssetManager>();
                var settings = x.Exchange.Resolve<ISettings>();
                string S(SystemTextId id) => assets.LoadString(id, settings.Language);

                x.Exchange.Attach(new Frame(x._width, x._height));
                var header = new Header(0, 0, x._width - 2, 1, S(SystemTextId.MainMenu_MainMenu));
                var divider = new Divider();
                var buttons = new[]
                    {
                    new Button(0, 0, x._width-2, 1, S(SystemTextId.MainMenu_ContinueGame)),
                    new Button(0, 0, x._width-2, 1, S(SystemTextId.MainMenu_NewGame)),
                    new Button(0, 0, x._width-2, 1, S(SystemTextId.MainMenu_LoadGame)),
                    new Button(0, 0, x._width-2, 1, S(SystemTextId.MainMenu_SaveGame)),
                    new Button(0, 0, x._width-2, 1, S(SystemTextId.MainMenu_Options)),
                    new Button(0, 0, x._width-2, 1, S(SystemTextId.MainMenu_ViewIntro)),
                    new Button(0, 0, x._width-2, 1, S(SystemTextId.MainMenu_Credits)),
                    new Button(0, 0, x._width-2, 1, S(SystemTextId.MainMenu_QuitGame)),
                };
            })
        };

        Action _menuFunc;
        int _width = 9;
        int _height = 14;

        public MainMenu() : base(Handlers) { _menuFunc = PrimaryMenu; }

        void PrimaryMenu()
        {
            bool gameInProgress = false;
            const int savedGames = 0;
            ImGui.Begin("Main Menu");
            if (savedGames > 0)
            {
                if (ImGui.Button("Continue game"))
                {
                }
            }

            if (ImGui.Button("New Game"))
            {
                // TODO
            }

            if (ImGui.Button("Load Game"))
                _menuFunc = LoadGameMenu;

            if (gameInProgress)
            {
                if (ImGui.Button("Save Game"))
                    _menuFunc = SaveGameMenu;
            }

            if (ImGui.Button("Options"))
                _menuFunc = OptionsMenu;

            if (ImGui.Button("View Intro"))
            {
                // TODO
            }

            if (ImGui.Button("Credits"))
            {
                // TODO
            }

            if (ImGui.Button("Quit"))
                Raise(new QuitEvent());

            ImGui.End();
        }

        void OptionsMenu()
        {
            int musicVolume = 64, fxVolume = 64, windowSize3d = 100, combatDetailLevel = 5, combatTextDelay = 10;
            ImGui.SliderInt("Music Volume", ref musicVolume, 0, 127);
            ImGui.SliderInt("Fx Volume", ref fxVolume, 0, 127);
            ImGui.SliderInt("3D Window Size", ref windowSize3d, 0, 100);
            ImGui.SliderInt("Combat Detail Level", ref combatDetailLevel, 1, 5);
            ImGui.SliderInt("Combat Text Delay", ref combatTextDelay, 1, 50);

            if (ImGui.Button("Back"))
                _menuFunc = PrimaryMenu;
        }

        void LoadGameMenu()
        {
            for (int i = 0; i < 10; i++)
            {
                if (ImGui.Button($"SaveName{i}"))
                {
                    // TODO
                }
            }

            if (ImGui.Button("Back"))
                _menuFunc = PrimaryMenu;
        }

        void SaveGameMenu()
        {
            for (int i = 0; i < 10; i++)
            {
                if (ImGui.Button($"SaveName{i}"))
                {
                    // TODO
                }
            }

            if (ImGui.Button("Back"))
                _menuFunc = PrimaryMenu;
        }
    }
}
