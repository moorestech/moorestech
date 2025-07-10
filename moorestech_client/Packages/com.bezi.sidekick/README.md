# Bezi Sidekick

Your personal assistant for Unity development.

## Installation

1. Unzip the package into a folder on your computer.

2. Open up Unity and go to `Window > Package Manager`.

3. Click the `➕` button and select `Add package from disk...`.

4. Navigate to the folder where you unzipped the package and select the `package.json` file.

5. Click `Open` and the package will be installed.

## Usage

You can open the Sidekick window by going to `Bezi > Sidekick` in the menu bar.

Alternatively, you can use the keyboard shortcut `alt + s` (has no default keybinding conflict, has great keyboard ergonomics, and has 'S' for Sidekick).

On first open you'll be prompted to enter your Unity Integration Token for Bezi. You can find this token in the integrations menu on [bezi.com](https://bezi.com). Copy and paste the token into the input field and click `Save`.

Using Sidekick is as simple as typing your prompt into the text area and hitting `Shift + Enter` or clicking the `Submit` button to send the prompt to Bezi Sidekick. You can additionally use the Commands for enhanced functionality (like auto fixing script errors, editing your scene, generating assets, etc.).

## Features

- **Scene Inspection**: In order to help you accomplish your tasks, Sidekick can inspect your scene hierarchy, Components, and Scripts. It also knows about the objects you currently have selected. It can answer questions like:

  - How does this character controller work?
  - What does the `PowerUp` script do?
  - What are the duplicate objects in my scene?

- **Create Objects**: Sidekick can create objects in your scene based on your prompt. For example, typing `Create a cube` will create a cube in your scene.

  - Supported Components:
    - MeshFilter
    - MeshRenderer
    - BoxCollider
    - SphereCollider
    - CapsuleCollider
    - Rigidbody
    - Custom MonoBehaviours in your project

- **Create Assets**: Sidekick can add assets to your project. Currently only Material additions are supported. Let us know which Asset types are of most importance so we can add support for these types.

- **Modify Objects**: Sidekick can modify or remove objects in your scene based on your prompt. It can update their transforms, reparent them, rename them, and change their components.

- **Fix Code**: Sidekick can fix code for you. Simply select an error in the Console and Sidekick will automatically update to the `/fix` command and autofill the error into the prompt. Now you can update the prompt or just Submit it.

- **Write Code**: Sidekick can write code for you based on your prompt. It'll also try to create an example in the scene if you ask it to. Code files are automatically saved to your project in the `Assets/Bezi Sidekick/Scripts` folder.

## Security and Data Privacy

TBD (e.g. Bezi Sidekick does not train on private code.)
