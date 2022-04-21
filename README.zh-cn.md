# IntelliSenseLocalizer
用于生成本地化IntelliSense文件的工具。

## 简介
在`.net6`之前，我们可以在这个页面 - [Download localized .NET IntelliSense files](https://dotnet.microsoft.com/en-us/download/intellisense)下载本地化的智能感知文件。但`.net6`发布很长一段时间后，这个页面也没有添加`.net6`的本地化的智能感知文件。根据`dotnet/docs`中的这个[issue](https://github.com/dotnet/docs/issues/27283)，里面说不再提供本地化智能感知文件了 - "`Yes, unfortunately, we will no longer be localizing IntelliSense.`"。但是[在线文档](https://docs.microsoft.com)里面还有本地化描述。所以有了这个工具。

`IntelliSenseLocalizer`使用[在线文档](https://docs.microsoft.com)生成本地化智能感知文件。工具会下载所有的api页面并分析页面以匹配原始的智能感知文件，然后生成目标`xml`。

得益于[在线文档](https://docs.microsoft.com)良好的本地化和统一的页面布局。这个工具理论上可以生成所有区域的智能感知文件。但是`如果页面布局变动了，这个工具无法自动的适配新的布局`。

## 如何使用

### 1. 安装本工具
```shell
dotnet tool install -g islocalizer
```

### 2. 尝试从github安装已生成好的智能感知文件

这个命令将尝试从github找到并安装`zh-cn`的`net6.0`智能感知包:

```shell
islocalizer install auto -m net6.0 -l zh-cn
```
你也可以使用`-cc`来指定内容双语对照类型
```shell
islocalizer install auto -m net6.0 -l zh-cn -cc LocaleFirst
```

### 3. 自己构建本地化智能感知文件

构建`net6.0`相关的文件:
```shell
islocalizer build -m net6.0
```
这个命令可能会运行很久。。。不过缓存完文件后，第二次生成会快很多。
生成的压缩包将会存放到默认输出目录，可以在控制台输出中找到路径。

### 4. 安装生成的智能感知文件
```shell
islocalizer install {ArchivePackagePath}
```
`ArchivePackagePath` 是build命令输出的路径.

#### 运行 `islocalizer -h` 可以看到更多的命令和帮助信息.