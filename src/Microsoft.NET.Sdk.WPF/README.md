# Microsoft.NET.Sdk.WPF
The `Microsoft.NET.Sdk.WPF` MSBuild project SDK is a variant on the `Microsoft.NET.Sdk` SDK used for .NET Core and .NET Standard projecs. It contains additional logic required for a project that uses WPF to build and be debuggable, and enables designer support for XAML files. To use this SDK, simply replace the value of the `<Project Sdk="..." />` attribute with `Microsoft.NET.Sdk.WPF`. No additional modifications to the project are required; you don't even need to add explicit imports to the project file so the `.tmp_proj` file will compile correctly any more, as the SDK now does that for you.

# Acknowledgments

This project contains logic originally from the [`MSBuild.Sdk.Extras` nuget package](https://github.com/onovotny/MSBuildSdkExtras). The license for this project is copied below.

## The MIT License (__MIT__)

### Copyright (c) Oren Novotny

_Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:_

_The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software._

__THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.__
