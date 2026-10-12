# Registro da entrega 1.2.0

## Classificação

Minor após 1.1.0: Biblioteca MP4, controle completo do MPV e remodelagem UX/UI
compatíveis com os dados, configurações e conteúdos existentes. Correções
de layout e rolagem entram na mesma entrega; não justificam 2.0.0.

## Aceite e verificação

- [x] Operador aprovou novamente a rolagem em 11/10/2026 e autorizou publicar.
- [ ] Testar roda lenta/rápida, inversão de direção, arraste da barra e teclado.
- [ ] Conferir programas, Biblioteca, detalhes e Configurações, claro/escuro.
- [ ] Conferir Programação desta sessão na navegação e lista em memória.

As demais verificações físicas continuam recomendadas; não foram presumidas
apenas pela execução de testes automatizados. A publicação foi autorizada
explicitamente após o teste manual de rolagem.

Instalador local gerado com sucesso em `.release/installer/Sinalo-Setup-win-x64.exe`,
com checksum SHA-256. O projeto e os executáveis do pacote estão em 1.2.0.
MPV completo (28 arquivos) e componentes de download foram validados no publish.
Para testar sem instalar, o executável está em `.release/Sinalo-win-x64/Sinalo.App.exe`.

## Preparação técnica e documentação

- As três entregas commitadas foram integradas e enviadas à main (377094b).
- PRs #20/#21/#22 mesclados; CI da integração aprovado.
- Base atualizada com o PR #23 do Dependabot (executor xUnit 4.0.1),
  sem alteração funcional do aplicativo; validação repetida sobre essa base.
- Ajustes finais de scroll e programação incluídos na entrega autorizada.
- Changelog consolidado em 1.2.0, data 11/10/2026.
- Guia do site atualizado para Biblioteca, MPV e programação, sem avisos de prévia.
- Rodar skill sinalo-release: testes, cobertura >=75%, publish e Inno Setup.
- Alinhar projeto, notas, instalador, hash e tag à mesma versão.
- Pacote final: 353 testes aprovados; cobertura de linhas 88,38% e branches
  77,17%. A percepção de rolagem foi aprovada pelo operador.

## Depois do aceite

1. Finalizar data do changelog e os avisos do site para a entrega publicada.
2. Atualizar roadmap/revisão com os resultados e manter melhorias futuras.
3. Regerar o candidato se houve mudança após a geração anterior.
4. Commitar e enviar main, conferir CI final.
5. Criar/enviar tag anotada v1.2.0; conferir Action e instalador no release.
6. Conferir atualização a partir da 1.1.0, preservando banco e vídeos.

A validação automatizada não comprova fluidez em hardware com HD e DPI real.
