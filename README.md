# IntelliSenseLocalizer [中文文档](./README.zh-cn.md)
a tool for generate and install Localized IntelliSense files. 用于生成和安装本地化IntelliSense文件的工具。

## intro
Before `.net6` we can download the localized .NET IntelliSense files from this page - [Download localized .NET IntelliSense files](https://dotnet.microsoft.com/en-us/download/intellisense). But long after `.net6` was released. This page did not add the localized .NET IntelliSense files for `.net6`. According to this [issue](https://github.com/dotnet/docs/issues/27283) in `dotnet/docs` there is no localized .NET IntelliSense files any more - "`Yes, unfortunately, we will no longer be localizing IntelliSense.`". But the [online docs](https://docs.microsoft.com) has the localized descriptions. So this tool was born.

`IntelliSenseLocalizer` use the [online docs](https://docs.microsoft.com) to generate Localized IntelliSense files. This tool will download all api pages and analysis the content to match the origin IntelliSense file content and generate the target `xml`.

Benefit from [online docs](https://docs.microsoft.com)'s nice localization and uniform page layout. This tool can theoretically generate all locale's file. But `when the page layout change this tool cannot automatically adapt to the new layout`.

## How to use

### 1. install the tool
```shell
dotnet tool install -g islocalizer
```

#### run `islocalizer -h` to see more command and helps.

Append the argument -h at the end of the command to view the help of the command. eg:
```shell
islocalizer install auto -h
islocalizer cache -h
```

### 2. try install Localized IntelliSense files from nuget.org

#### View available packs [Nuget](https://www.nuget.org/packages/IntelliSenseLocalizer.LanguagePack)

This command try get the Localized IntelliSense files from nuget.org what moniker is `net6.0` and locale is `zh-cn`. And install it:

```shell
islocalizer install auto -m net6.0 -l zh-cn
```
Also you can set the ContentCompareType by `-cc`
```shell
islocalizer install auto -m net6.0 -l zh-cn -cc LocaleFirst
```

### 3. build the local Localized IntelliSense files yourself

build files about `net6.0`:
```shell
islocalizer build -m net6.0
```
This command may take a whole day... But when cached all page it will be completed faster.
The archive package will be saved in the default output directory. You can found the path in console.

### 4. install builded file
```shell
islocalizer install {ArchivePackagePath}
```
`ArchivePackagePath` is the path of the archive package that you builded.
