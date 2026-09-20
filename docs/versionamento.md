# Versionamento e releases

O Sinalo usa o formato `MAJOR.MINOR.PATCH`, conhecido como versionamento
semantico. O numero identifica a entrega que o operador instalou e deve ser o
mesmo no aplicativo, no changelog, na tag Git e na release do GitHub.

## Quando incrementar cada numero

- **Patch** (`0.1.12` para `0.1.13`): correcao compativel, sem alterar o uso
  esperado das configuracoes ou dos dados existentes.
- **Minor** (`0.1.13` para `0.2.0`): funcionalidade nova compativel, que nao
  exige migracao do operador.
- **Major** (`0.1.13` para `1.0.0`): marco de estabilidade para uso continuo
  ou uma mudanca que deixa de ser compativel com comportamento anterior.

O `0` inicial indica que o aplicativo ainda esta em evolucao. Isso nao reduz a
necessidade de preservar videos, configuracoes e fluxos existentes em cada
atualizacao.

## Fluxo de uma release

1. Atualize o `CHANGELOG.md` com as mudancas perceptiveis pelo operador.
2. Execute `./releaser.ps1 -NextPatch` para validar testes, cobertura de pelo
   menos 75% e gerar o instalador local. O script atualiza a versao do projeto
   somente quando a geracao termina com sucesso.
3. Revise o instalador em `.release/installer` e os arquivos alterados.
4. Faça o commit, envie o branch `main` e crie a tag anotada `vX.Y.Z` com a
   mesma versao do projeto.
5. A tag aciona o GitHub Actions, que gera o instalador novamente e publica a
   release com o checksum SHA-256.

Uma tag ou release ja publicada e imutavel. Se uma correcao for necessaria,
publique uma nova versao em vez de substituir arquivos de uma entrega que ja
chegou aos computadores da igreja.
