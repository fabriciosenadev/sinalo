# Versionamento e releases

O Sinalo usa versionamento semântico no formato `MAJOR.MINOR.PATCH`. O número
identifica a entrega instalada e deve coincidir no aplicativo, no changelog,
na tag Git e na release do GitHub.

A versão `1.0.0` marca o início da série estável do Sinalo. Ela reúne o produto
que já vinha sendo usado e evoluído na série `0.x`; não representa, por si só,
uma nova funcionalidade nem uma promessa de ausência de defeitos. A partir
dela, as versões seguem estas regras:

## Quando incrementar cada numero

- **Patch** (`1.0.0` para `1.0.1`): correção compatível que preserva os dados
  e o uso esperado das configurações existentes.
- **Minor** (`1.0.0` para `1.1.0`): funcionalidade nova compatível, que não
  exige migração do operador.
- **Major** (`1.0.0` para `2.0.0`): mudança incompatível que exige alteração
  no uso esperado, nos dados ou nas configurações.

Mesmo em uma mudança major, a atualização deve explicar claramente qualquer
migração necessária. Vídeos, configurações e fluxos existentes devem ser
preservados sempre que possível.

## Fluxo de uma release

1. Atualize o `CHANGELOG.md` com as mudanças perceptíveis pelo operador e
   revise a documentação pública relacionada.
2. Para uma versão normal de correção, execute `./releaser.ps1 -NextPatch`.
   Para uma versão minor, major ou diferente da próxima patch, informe o número
   explicitamente, por exemplo `./releaser.ps1 -Version 1.0.0`.
3. O processo executa os testes, exige pelo menos 75% de cobertura de linhas e
   ramificações, publica para `win-x64` e gera o instalador Inno Setup.
4. Revise o instalador em `.release/installer`, o checksum e os arquivos
   alterados. A versão explícita precisa estar registrada em
   `src/Sinalo.App/Sinalo.App.csproj` antes da geração.
5. Faça o commit, envie o branch `main` e crie a tag anotada `vX.Y.Z` com a
   mesma versão do projeto.
6. A tag aciona o GitHub Actions, que gera o instalador e publica a release
   com o checksum SHA-256. Confirme a execução e os dois arquivos anexados.

Uma tag ou release já publicada é imutável. Se uma correção for necessária,
publique uma nova versão em vez de substituir arquivos de uma entrega que já
chegou aos computadores da igreja.
