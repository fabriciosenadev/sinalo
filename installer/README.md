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
