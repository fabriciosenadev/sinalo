# Compatibilidade de Provai e Vede — 27/10/2026

## Escopo implementado, ainda não publicado

- Endereço padrão e configuração existentes preservados.
- Endereço dinâmico em Libras:
  `https://www.adventistas.org/pt/mordomiacrista/projeto/provai-e-vede/#libras`.
  O fragmento seleciona Libras no Sinalo; os links publicados na página base
  determinam o trimestre e ano atuais. Não presume o endereço de uma página
  futura nem baixa convencional/antigo quando a versão solicitada não existe.
- Página trimestral direta convencional ou Libras, com ano e trimestre da URL.
- IDs convencionais legados preservados; Libras com IDs e nomes de arquivo distintos.
- Identificação `(Libras)` no título; nenhuma nova categoria ou entrada de menu.
- Downloads automáticos, pré-cálculo de espaço e seleção manual da fila limitados
  à descoberta do endereço do pedido. O catálogo anterior não é apagado.
- Mantidas regras de sábados, trimestre inteiro, arquivo existente e nova
  elegibilidade após exclusão do arquivo local.
- Documentação técnica, changelog em desenvolvimento e guia do site atualizados.

## Evidências obtidas

- Testes filtrados por `ProvaiEVede`: **29 aprovados**, incluindo SQLite e
  arquivos temporários; troca convencional/Libras nos dois sentidos,
  histórico, fixação, nomes iguais, repetição, exclusão e mudança de ano.
- Consulta real executando o conector: **13 MP4** na página base padrão e
  **13 MP4** na página trimestral Libras, de 03/10/2026 a 26/12/2026.
  Nenhum vídeo real foi baixado e nenhuma configuração do usuário foi alterada.
- Ampliação dinâmica: **39 testes específicos aprovados**, incluindo virada
  de ano e trimestre com URL preservada em SQLite e acervo anterior mantido.
  Consulta real da base com `#libras`: **13 MP4 em Libras**, com os mesmos IDs
  da página trimestral direta. Suíte completa da ampliação: **463 aprovados,
  0 falhas, 0 ignorados**, com **89,10% de linhas** e **78,28% de branches**;
  meta de 75% aprovada com SDK 10.0.403, sem alterações de limite.
- `git diff --check` sem erros.
- Retomada após atualização do ambiente: SDK **10.0.403**, compatível com o
  `global.json` existente, sem alteração de configuração do projeto.
- Nova execução selecionada: **34 testes aprovados**, incluindo os quatro
  cenários da fila MainWindow e o teste MPV que havia falhado.
- Suíte completa com `eng/test-coverage.ps1 -Configuration Release`:
  **457 aprovados, 0 falhas, 0 ignorados**; cobertura total de **89,10% de linhas**
  e **78,26% de branches**. Meta de 75% aprovada sem redução de limites.

Páginas consultadas:

- https://www.adventistas.org/pt/mordomiacrista/projeto/provai-e-vede/
- https://www.adventistas.org/pt/mordomiacrista/projeto/provai-e-vede/#libras
- https://downloads.adventistas.org/pt/possibilidades/video/provai-e-vede-2026-4o-trimestre-libras/

## Histórico da interrupção — resolvida

- A primeira suíte completa executou **453 testes: 452 aprovados e 1 falhou**.
  Falha em `MpvControllerTests.ControlsConfirmedPlaybackAndReusesProcessWithoutActivityGap`:
  posição 0,1 após seek, quando o teste esperava entre 1,5 e 2,5. Não houve
  alteração de código do MPV nesta demanda; a causa da falha não foi determinada.
- Foram acrescentados depois quatro cenários do fluxo da fila da MainWindow
  (automático/manual, convencional/Libras), agora compilados e aprovados.
- Ao tentar repetir a validação, o SDK .NET 10.0.402 usado inicialmente deixou
  de estar disponível: a pasta permaneceu, mas sem `dotnet.dll`. O host passou
  a listar apenas SDKs até 9.0.320. Não foi alterado `global.json`, instalado SDK
  ou reduzida a meta de cobertura para contornar o problema.
- O usuário confirmou que estava atualizando .NET/Visual Studio e solicitou
  aguardar. Após sua autorização de retomada, SDK, compilação, testes e cobertura
  foram validados conforme os resultados acima. O teste MPV passou na execução
  selecionada e na suíte completa, sem alteração de código ou teste do player;
  isso não determina a causa da falha inicial.

## Aceite manual

- O usuário informou que o teste realizado funcionou e autorizou os commits e o
  preparo da release como funcionalidade em 27/10/2026. Não detalhou quais etapas
  individuais executou; não se presume validação manual de toda a matriz automatizada.

## Limite operacional

A página base com `#libras` acompanha o trimestre e ano operacionais e depende
da publicação do link correto pelo site oficial. Não é preciso reconfigurar a
cada período. Uma URL trimestral direta continua fixa; não avança para o
trimestre seguinte. Com sábados selecionados, aplica as datas operacionais
atuais; trimestre inteiro considera os vídeos encontrados na página escolhida.
Não busca simultaneamente as duas versões e não muda configurações automaticamente.

Aceite recebido para commits e preparo da release. Publicação no GitHub é uma
etapa distinta do preparo local.
