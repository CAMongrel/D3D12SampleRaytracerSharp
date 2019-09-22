# D3D12 Sample Raytracer in C#
This project is mostly a line-by-line translation of the sample source code at https://github.com/NVIDIAGameWorks/DxrTutorials/ to C# using Vortice.Windows as the backend.  
It essentially covers part 14 ("Refit") of the sample code, but has some additional changes to make the triangles receive shadows as well.

## Requirements
The basic requirements are the same as in the above mentioned repository:
- A GPU that supports DXR (Such as NVIDIA's Volta or Turing hardware)
- Windows 10 RS5 (version 1809)
- Windows 10 SDK version 1809 (10.0.17763.0)
- Visual Studio 2019 (the included solution is for 2019, but 2017 likely works as well)

## Screenshot
![Screenshot](/Screenshots/image.jpg?raw=true)

## License
The MIT License (MIT)

Copyright (c) 2019 Henning Thoele

Permission is hereby granted, free of charge, to any person obtaining a
copy of this software and associated documentation files (the "Software"),
to deal in the Software without restriction, including without limitation
the rights to use, copy, modify, merge, publish, distribute, sublicense,
and/or sell copies of the Software, and to permit persons to whom the
Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL
THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
DEALINGS IN THE SOFTWARE.
