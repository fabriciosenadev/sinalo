# Aceite manual do candidato: categorias personalizadas

Branch: feature/custom-media-categories. Base publicada: 1.2.0.
Status: aceite da exclusão confirmado pelo usuário em 16/10/2026; publicação da
1.3.0 autorizada. A matriz física abaixo não foi integralmente certificada.

Atualização de 16/10/2026: ajustes de usabilidade/robustez implementados; resultados
e pendências atuais em [validação da estabilização](validacao-estabilizacao-categorias-2026-10-16.md).
Os números abaixo registram a rodada original, não a suíte mais recente.

Verificação automatizada: 375 testes aprovados, cobertura 87,49% linhas e 75,74%
branches. Render WPF do gerenciamento e seleção múltipla exercitados; isso não
comprova os cenários físicos abaixo nem download real pela internet da igreja.

## Roteiro do operador

1. Criar Louvores e Culto jovem em Minhas categorias → Criar / gerenciar categorias.
2. Renomear/ordenar; verificar nome preservado após fechar/abrir. Nome duplicado,
   vazio ou igual ao de programa deve ser recusado.
3. Importar um MP4 como cópia em Louvores e outro como referência em Culto jovem.
   Conferir categoria no menu/filtro e original intacto. Reimportar existente:
   deve oferecer movimentar cadastro, sem duplicar arquivo.
4. Adicionar vídeo por link ao destino personalizado, com/sem data. Aguardar
   download real completo e testar reprodução. Repetir URL não deve baixar novamente.
5. Mover para outra categoria, incluindo programa; conferir ID/caminho/histórico
   preservados e presença no destino. Seleção múltipla usa Ctrl/Shift.
6. Excluir categoria escolhendo Transferir: todas as cópias/referências e itens
   ausentes passam ao destino. Arquivos não mudam de pasta.
7. Em outra categoria, confirmar Excluir definitivamente: cópias somem, cadastros/
   programação são limpos e originais externos continuam intactos. Cancelar preserva tudo.
8. Testar arquivo em uso/erro de acesso: exclusão pendente deve ser visível; nova
   tentativa explícita completa, sem apagar arquivo substituído desde a confirmação.
9. Reprodução, download/importação ativos não devem ser interrompidos silenciosamente.
   Atualização deve aguardar operação destrutiva; pasta não muda com exclusão pendente.
10. Atualizar descoberta dos três programas: classificação manual e configuração
    legadas permanecem. Testar limpeza/fixação e migração da pasta após concluir exclusão.

Registrar build, ambiente e resultado por cenário. Cobrir claro/escuro,
DPI 100/125/150%, janela compacta/menu longo, HD/projetor, pendrive e instalação
limpa/atualização de banco legado. Os automatizados não substituem estes cenários.
Nenhum aceite físico foi presumido ou registrado como aprovado.
