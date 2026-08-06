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
