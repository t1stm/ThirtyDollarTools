# Thirty Dollar Converter CLI

The **Thirty Dollar Converter CLI** is a command-line interface for the Thirty Dollar Converter, allowing you to convert
Thirty Dollar Website compositions (.tdm or .txt) into high-quality WAVE audio files directly from your terminal.

## Features

- **Batch Processing**: Convert multiple sequences at once.
- **Customizable Sample Rate**: Export audio at any desired sample rate (default 48kHz).
- **Automatic Sample Downloading**: Downloads missing samples from the Thirty Dollar Website if they are not present
  locally.
- **Progress Visualization**: Real-time progress bar for each conversion.

## Usage

### Basic Conversion

```bash
ThirtyDollarConverter.CLI -i my_sequence.tdm
```

This will output `my_sequence.tdm.wav`.

### Specifying Output Path

```bash
ThirtyDollarConverter.CLI -i my_sequence.tdm -o output.wav
```

### Batch Conversion

```bash
ThirtyDollarConverter.CLI -i seq1.tdm seq2.tdm -o out1.wav out2.wav
```

### Custom Sample Rate

```bash
ThirtyDollarConverter.CLI -i my_sequence.tdm -s 44100
```

### Options

- `-i, --input`: (Required) The sequence locations.
- `-o, --output`: The exported audio locations.
- `-s, --sample-rate`: Changes the exported audio's sample rate.
- `--download-location`: Sets the directory where samples will be downloaded/loaded from (default: `./Sounds`).

## How to Build

1. Ensure you have the .NET 10.0 SDK installed.
2. Clone the repository.
3. Navigate to the root directory and run:
   ```bash
   dotnet build ThirtyDollarTools.sln
   ```
4. The compiled binary will be located in `Converter/ThirtyDollarConverter.CLI/bin/Debug/net10.0/`.

## Dependencies

- **ThirtyDollarConverter**: The core conversion engine.
- **CommandLineParser**: For handling terminal arguments.
- **Serilog**: For logging.
