# Gerar o instalador do Sinalo

## Pré-requisitos

- .NET SDK 10.
- [Inno Setup 6](https://jrsoftware.org/isinfo.php).

## Comando

Na raiz do projeto, execute:

```powershell
.\releaser.ps1 -NextPatch
```

O comando le a versao atual de `Sinalo.App.csproj`, incrementa o numero de patch e atualiza o arquivo ao concluir a geracao do instalador. Por exemplo, `0.1.1` passa a `0.1.2`. O arquivo alterado deve acompanhar o proximo commit da entrega.

## Politica de versao

O Sinalo usa o formato `MAJOR.MINOR.PATCH`:

- `PATCH` para correcoes compativeis, como `0.1.12` para `0.1.13`;
- `MINOR` para novas funcionalidades compativeis, como `0.1.13` para `0.2.0`;
- `MAJOR` para marcos de estabilidade ou mudancas incompativeis, como `1.0.0`.

Uma versao publicada nunca deve ser renomeada ou substituida. Depois da
validacao operacional de uma versao de correcao, uma nova release pode marcar
o inicio da serie estavel `1.0.0`.

Para gerar novamente a versao ja definida no projeto, sem incrementa-la:

```powershell
.\releaser.ps1
```

O processo executa os testes e a cobertura mínima, publica o aplicativo para Windows x64 e gera:

```text
.release\installer\Sinalo-Setup-win-x64.exe
```

Use `-SkipTests` somente para testes locais do empacotamento; uma distribuição para operadores deve sempre ser gerada com os testes e a cobertura executados.

## Dados do operador

O aplicativo é instalado em `C:\Program Files\Sinalo`. As configurações, catálogo e vídeos ficam em `%LocalAppData%\Sinalo` e não são removidos ao desinstalar o aplicativo.
