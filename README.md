# Ollama Vision Autopilot

This is a Windows application that acts as an AI-powered "autopilot" for your computer. It uses the vision and reasoning capabilities of a multimodal AI (via the Ollama API) combined with the Windows UI Automation framework to understand your screen and execute your commands.

## Features

-   **Natural Language Control:** Give instructions in plain English (e.g., "Open Notepad and type 'hello'").
-   **Vision and UI Context:** The application sends both a screenshot and a structured map of the UI elements (buttons, text boxes, etc.) to the AI, giving it a rich understanding of the current screen.
-   **Automated Action Execution:** The AI's decisions are translated into actions like clicking buttons and typing text directly into applications.
-   **Global Hotkeys:**
    -   `Ctrl + Alt + C`: Start or Stop the execution of the current instruction.
    -   `Ctrl + Alt + S`: A dedicated safety hotkey to immediately stop any running task.

## Requirements

-   Windows Operating System
-   .NET 8 Desktop Runtime
-   [Ollama](https://ollama.com/) installed and running locally.
-   A multimodal model pulled in Ollama (e.g., `llava`). You can pull it by running:
    ```bash
    ollama pull llava
    ```

## Setup

1.  **Build the Project:** Since this project was created without a build environment, you would typically open the `.sln` (solution) file in Visual Studio and build the project. This will produce an `.exe` file in the `bin/Debug` or `bin/Release` directory.
2.  **Ensure Ollama is Running:** Make sure the Ollama desktop application is running in the background.

## Usage

1.  **Run the Application:** Launch the `OllamaVision.exe` file.
2.  **Enter an Instruction:** In the main window, type the task you want the AI to perform into the "Instruction" text box. For example:
    -   `Find the calculator and add 2 and 2.`
    -   `Open notepad and write a short poem about AI.`
3.  **Start Execution:**
    -   Click the **"Execute (Ctrl+Alt+C)"** button.
    -   Alternatively, press the global hotkey `Ctrl + Alt + C` while the application is running.
4.  **Monitor Progress:** The application will begin executing the task step-by-step. The large text box at the bottom will show a log of the AI's reasoning and the actions it's taking.
5.  **Stop Execution:**
    -   While a task is running, the "Execute" button will change to "Stop Execution". Click it to cancel the task.
    -   You can also press `Ctrl + Alt + C` again to stop the task.
    -   For an emergency stop, you can use the dedicated `Ctrl + Alt + S` hotkey at any time.
