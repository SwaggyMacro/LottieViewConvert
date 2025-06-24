<p align="center">
    <img src="./images/toast.gif" width="128px">
</p>
<p align="center">
    <a href="readme_cn.md"><img src="https://img.shields.io/badge/Lang-简体中文-red"></a>
    <img src="https://img.shields.io/badge/version-1.1.6-yellow">
    <a href="//github.com/SwaggyMacro/LottieViewConvert"><img src="https://img.shields.io/badge/Repo-LottieViewConvert-green"></a>
</p>
<p align="center">
    <img src="./images/main_interface.png" width="512px">
</p>

## 🎬 Lottie & TGS Animation Converter

A powerful cross-platform desktop application for converting TGS (Telegram Stickers) and Lottie animations to various formats including GIF, WebP, APNG, MP4, MKV, AVIF, and WebM.

### ✨ Features
---
- **Multiple Format Support**: Convert to GIF, WebP, APNG, MP4, MKV, AVIF, WebM
- **Batch Processing**: Convert multiple files simultaneously
- **TGS & Lottie Support**: Handle both Telegram sticker/emoji (.tgs) and standard Lottie files (.json, .lottie)
- **Telegram Integration**: Parse and download Telegram sticker/emoji set directly
- **Customizable Output**: Adjust playback speed, frame rate, resolution, and conversion quality
- **Cross-Platform**: Works on Windows, macOS, and Linux
- **Modern UI**: Built with SukiUI for a sleek, responsive interface
- **Automatic Installation of Dependencies**: Simplify setup by automating gifski and FFmpeg installation
- **Concurrent tasks**: support for faster batch conversion
---

### 📝 System Requirements
---
Install the following dependencies and ensure they are available in your PATH:

#### Required Dependencies
- **[gifski](https://gif.ski)** - For high-quality GIF conversion
- **[FFmpeg](https://ffmpeg.org)** - For video format conversion (MP4, MKV, WebM)

#### Installation Instructions

Now, You can `install it automatically` by the application, or you can install it manually.
Just run the application, and go to `Settings` -> `Dependencies`, and it will install gifski and ffmpeg automatically.

`gifski` automatically install `only support x64 platforms`, if you are using ARM64 platform, please install it manually.

`Only tested on Windows, Ubuntu now.`

For **manual installation**, follow the instructions below based on your operating system:

**Windows:**
```bash
# Install via Chocolatey
choco install gifski ffmpeg

# Or download directly:
# gifski: https://gif.ski/
# ffmpeg: https://ffmpeg.org/download.html
```

**macOS:**
```bash
# Install via Homebrew
brew install gifski ffmpeg
```

**Linux (Ubuntu/Debian):**
```bash
# gifski
sudo snap install gifski
# or
cargo install gifski

# ffmpeg
sudo apt update
sudo apt install ffmpeg
```

**Verify Installation:**
```bash
gifski --version
ffmpeg -version
```

### 🚀 Getting Started
---

#### 1. Download
- Download the latest release from the [Releases](https://github.com/SwaggyMacro/LottieViewConvert/releases) page
- Extract the archive to your preferred location
- Run the application executable

#### 2. Basic Usage
##### Single Conversion
1. **Launch the application**
2. **Select source files**: Click "Browser" right the Home Page,  or drag & drop TGS/Lottie files
3. **Choose output format**: Select from GIF, WebP, APNG, MP4, MKV, AVIF, WebM
4. **Adjust settings** (optional):
    - Frame rate (1-240 fps, 100 fps for GIF only)
    - Resolution
    - Playback speed (0.1x - 10.0x)
    - Quality settings
5. **Convert**: Click "Convert"

##### Batch Conversion
1. **Launch the application**
2. **Go to the Factory Page**
3. **Browser Tgs/Lottie Folder**: Select a folder containing TGS or Lottie files, or just drag & drop the folder.
4. **Adjust settings like single conversion**
5. **Convert**: Click "Start"

##### Download Telegram Sticker/Emoji (Including animated stickers and regular stickers)
1. **Launch the application**
2. **Set up the Telegram Bot Token**: Go to `Settings` -> `Telegram`, and enter your bot token.
   - You can get a bot token by creating a bot with [BotFather](https://t.me/botfather) on Telegram.
   - Set up the proxy if needed in `Settings` -> `Proxy`.
3. **Go to the Tgs Download Page**
4. **Enter the sticker/emoji set URL**: Paste the Telegram sticker/emoji set URL (e.g., `https://t.me/addstickers/Godzi`)
5. **Download**: Click "Download" to fetch all stickers in the set


#### 3. Advanced Features
- **Quality Presets**: Choose from Low, Medium, High, or Custom quality settings
- **Batch Operations**: Queue multiple conversions
- **Download Telegram Stickers Directly**: Fetch sticker sets from Telegram using the bot token
- **Preview**: Real-time preview of animations before conversion
- **Progress Tracking**: Monitor conversion progress for each file

### 🖼️ Screenshots
---
![Main Interface](./images/main_interface.png)

https://github.com/user-attachments/assets/b8afe3d4-2301-4c07-9735-6a5238922f6b

### 📋 Supported Formats
---

#### Input Formats
- `.tgs` - Telegram Sticker files
- `.json` - Lottie animation files

#### Output Formats
- `.gif` - Animated GIF
- `.webp` - Animated WebP
- `.apng` - Animated PNG
- `.mp4` - MP4 Video
- `.mkv` - Matroska Video
- `.avif` - AV1 Image File Format
- `.webm` - WebM Video

### 🔧 Build from Source
---

#### Prerequisites
- .NET 8.0 SDK
- Visual Studio 2022 or JetBrains Rider

#### Build Steps
```bash
git clone https://github.com/SwaggyMacro/LottieViewConvert.git
cd LottieViewConvert
dotnet restore
dotnet build --configuration Release
dotnet run --project LottieViewConvert
```

#### Platform-Specific Builds
```bash
# Windows
dotnet publish -c Release -r win-x64 --self-contained

# macOS
dotnet publish -c Release -r osx-x64 --self-contained

# Linux
dotnet publish -c Release -r linux-x64 --self-contained
```

### 🛠️ Tech Stack
---
- **Framework**: C# with Avalonia UI
- **Architecture**: ReactiveUI for MVVM pattern
- **Rendering**: SkiaSharp Skottie for Lottie animation rendering
- **WebP Processing**: ImageMagick
- **Dependencies**: gifski, FFmpeg


### 🤝 Contributing
---
Contributions are welcome! Please feel free to submit a Pull Request. For major changes, please open an issue first to discuss what you would like to change.

1. Fork the repository
2. Create your feature branch (`git checkout -b feature/AmazingFeature`)
3. Commit your changes (`git commit -m 'Add some AmazingFeature'`)
4. Push to the branch (`git push origin feature/AmazingFeature`)
5. Open a Pull Request

### 📄 License
---
This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

### 🔗 Related Projects
---
- [lottie-converter](https://github.com/ed-asriyan/lottie-converter) - Render After Effects animations on the web
- [rlottie](https://github.com/Samsung/rlottie) - A platform independent standalone library
- [gifski](https://github.com/ImageOptim/gifski) - GIF encoder based on libimagequant
- [FFmpeg](https://github.com/FFmpeg/FFmpeg) - A complete solution to record, convert and stream audio and video
- [SkiaSharp](https://github.com/mono/SkiaSharp) - .NET bindings for Skia

### 🙏 Acknowledgments
---
- **Lottie** by Airbnb for the animation format
- **Telegram** for the TGS format
- **gifski** team for the excellent GIF encoder
- **FFmpeg** community for video processing capabilities
- **Avalonia** team for the cross-platform UI framework

### 📞 Support
---
If you encounter any issues or have questions:
- 📝 [Open an issue](https://github.com/SwaggyMacro/LottieViewConvert/issues)
- 💬 [Start a discussion](https://github.com/SwaggyMacro/LottieViewConvert/discussions)
- 📧 Email: [your-email@example.com]

---
<p align="center">Made with ❤️ by <a href="https://github.com/SwaggyMacro">SwaggyMacro</a></p>
