# Estratégia de testes

## Meta obrigatória

O Sinalo exige pelo menos **75% de cobertura de linhas e branches** no código de produção. A cobertura não será tratada como uma métrica exclusiva de testes unitários.

## Pirâmide de testes

| Tipo | Escopo | Exemplos |
| --- | --- | --- |
| Unitário | Regra isolada, rápida e sem I/O | cálculo de sábados, trimestre, seleção da janela e estado de um item |
| Integração | Componentes reais com recursos temporários | SQLite, estrutura de diretórios, arquivo `.part`, validação SHA-256 e conectores HTTP simulados |
| Ponta a ponta | Fluxo completo, isolado e sem fontes reais | configurar uma fonte, descobrir item, sincronizar arquivo de teste e pedir reprodução local |

Os testes ficam em `tests/Sinalo.Tests/Unit`, `Integration` e `EndToEnd`. Cada teste de integração ou ponta a ponta deve criar seus próprios diretórios temporários e removê-los ao fim da execução.

## Execução local

```powershell
.\eng\test-coverage.ps1
```

O script executa os testes, gera `TestResults\coverage.cobertura.xml` e falha quando a cobertura total de linhas ou branches for inferior a 75%.

## Regras

- Não excluir código de produção da cobertura para elevar artificialmente a métrica.
- Não depender de internet, arquivos de fontes reais ou o perfil do usuário nos testes automatizados.
- Todo bug corrigido deve receber um teste que reproduza a falha anterior.
- Recursos externos, como HTTP, VLC e FFmpeg, devem ser encapsulados por contratos para permitirem doubles de teste.
- A fila de sincronizacao deve ter testes para serializacao, deduplicacao, cancelamento e continuidade apos falha; ao menos um teste de integracao deve confirmar que fontes diferentes nao executam downloads simultaneos.

## UX/UI — branch de experiência

`ExperienceLayoutTests` instancia e renderiza as telas WPF em STA nos dois temas,
1280×720, 1920×1080 e janela mínima; escalas por LayoutTransform exercitam
restrições de espaço, mas não substituem DPI real. As imagens são geradas em
`TestResults/ux-ui` (não versionadas).

A suíte verifica espaço útil do catálogo, ação final do link, regras antigas,
validação de campos, encaminhamento de comandos, áudio global, thumb/track da
rolagem e preservação da cena ao navegar. `WorkspaceExperienceTests` cobre
contexto, seleção, programação e novidades. Rede, áudio e saída físicos são
substituídos por doubles nos cenários funcionais apropriados.

Conferir em uso real: DPI do Windows, teclado/leitor de tela, monitor principal
e projetor, atualização instalada e máquina com HD. Ver roteiro em
[execução UX/UI](ux-ui/execucao.md). Não transformar testes simulados em aceite
manual nem reduzir a meta de 75% para acomodar o redesign.
