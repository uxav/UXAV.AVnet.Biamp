<p align="center">
   <img src="avnet_logo.svg">
</p>

<h1 align="center">AVnet Biamp Library</a></h1>

<p align="center">An open-source Crestron Biamp Library for use in AVnet Framework</p>

<p align="center">
   <img alt="Status" src="https://img.shields.io/github/actions/workflow/status/uxav/UXAV.AVnet.Biamp/test.yml?branch=main&style=flat&logo=github&label=status">
   <img alt="NuGet Version" src="https://img.shields.io/nuget/v/UXAV.AVnet.Biamp?style=flat&logo=nuget">
   <img alt="Downloads" src="https://img.shields.io/nuget/dt/UXAV.AVnet.Biamp?style=flat&logo=nuget">
   <img alt="Issues" src="https://img.shields.io/github/issues/uxav/UXAV.AVnet.Biamp?style=flat&logo=github">
   <img alt="Pull Requests" src="https://img.shields.io/github/issues-pr/uxav/UXAV.AVnet.Biamp?style=flat&logo=github">
   <img alt="Licnse" src="https://img.shields.io/github/license/uxav/UXAV.AVnet.Biamp?style=flat">
</p>

## Index

- [Quick start](#quick-start)
- [Links](#links)
- [Dependencies](#dependencies)
- [Release Notes](#release-notes)
- [Contributing](#contributing)
- [License](LICENSE)

## Quick Start

To use this test library in your project, follow these steps:

1. Install the package via NuGet. You can use the following command in the Package Manager Console:

   ```
    dotnet add [<PROJECT>] package UXAV.AVnet.Biamp
   ```

2. Import the library classes in your code file(s):

   ```csharp
   // Import NVX from AVnet
   using UXAV.AVnet.Biamp;
   ```

3. Create your Biamp device:

   ```csharp
   // create instance of tesira
   var tesira = new Tesira("My Tesira", "192.168.10.10", "default", "password");

   // register a control block
   var levelBlock = (LevelControlBlock)tesira.RegisterControlBlock(TesiraBlockType.LevelControlBlock, "LEVEL_INSTANCE_TAG");

   // subscribe for changes
   levelBlock.Subscribe();
   ```

## Links

GitHub Repository: [UXAV.AVnet.Biamp](https://github.com/uxav/UXAV.AVnet.Biamp)

NuGet Package: [UXAV.AVnet.Biamp](https://www.nuget.org/packages/UXAV.AVnet.Biamp)

## Dependencies

- [Crestron.SimplSharp.SDK.ProgramLibrary](https://www.nuget.org/packages/Crestron.SimplSharp.SDK.ProgramLibrary)
- [UXAV.AVnet.Core](https://www.nuget.org/packages/UXAV.AVnet.Core)

## Release Notes

### v2.0.0

- Reconfigured workspace to new style SDK format and added support for .NET 6.0

## Contributing

Contributions are welcome! If you would like to contribute to this project, please follow these guidelines:

1. Fork the repository.
2. Create a new branch for your feature or bug fix.
3. Make your changes and commit them.
4. Push your changes to your forked repository.
5. Submit a pull request to the main repository.

Please ensure that your code follows the project's coding conventions and includes appropriate tests.

- For feature branches use the name `feature/feature-name`
- Version numbers are checked against existing tags and fail CI on match

Thank you for your interest in contributing to this project!

## License

This project is licensed under the [MIT License](LICENSE.md).
