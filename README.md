# Topaz.UrlShortener

Encurtador de URLs desenvolvido em C# e ASP.NET Core MVC com .NET 8 para o desafio técnico da Topaz.

A aplicação disponibiliza uma API REST e uma interface web para criar links curtos com alias personalizado ou código automático. A criação é processada por uma fila com um único consumidor; o acesso aos links realiza redirecionamento HTTP para o endereço original.

## Tecnologias

- C# / .NET 8 / ASP.NET Core MVC
- JavaScript com Fetch API
- Bootstrap do template MVC
- `System.Threading.Channels`
- `ConcurrentDictionary`
- NUnit

Não é necessário configurar banco de dados nem serviços externos.

## Funcionalidades

- Criar um link com alias opcional.
- Verificar a disponibilidade do alias durante a criação e retornar conflito se estiver ocupado.
- Gerar código automático de oito caracteres quando o alias não é informado.
- Redirecionar o link curto para a URL original.
- Copiar o link pela interface.
- Exibir mensagens de erro e estado de carregamento.

## Executar localmente

### Visual Studio

1. Instale o SDK do .NET 8 e uma versão do Visual Studio com suporte ao .NET 8 e à carga de trabalho de desenvolvimento ASP.NET e web.
2. Clone o repositório e abra `Topaz.UrlShortener.sln`.
3. Defina `Topaz.UrlShortener` como projeto de inicialização.
4. Execute com F5 ou Ctrl+F5.
5. Acesse o endereço informado pelo Visual Studio.

Durante o desenvolvimento, foi utilizado `https://localhost:7194`. Confira a porta do perfil de execução no seu ambiente. Para HTTPS local, aceite a configuração de confiança do certificado de desenvolvimento quando solicitada.

### Linha de comando

Na pasta que contém a solução:

```bash
dotnet restore
dotnet build --configuration Release
dotnet run --project Topaz.UrlShortener
```

Abra o endereço exibido no terminal. Para configurar a confiança do certificado HTTPS de desenvolvimento, quando necessário:

```bash
dotnet dev-certs https --trust
```

## Utilizar a interface

1. Informe uma URL absoluta com `http://` ou `https://`.
2. Informe um alias ou deixe o campo vazio para gerar um código.
3. Clique em **Gerar link curto**.
4. Abra o link retornado ou utilize **Copiar link**.

A cópia automática utiliza a API de clipboard do navegador, que depende de contexto seguro e das permissões do navegador. Se a cópia falhar, a interface orienta a cópia manual.

## API REST

### Criar link

`POST /api/links`

```http
POST https://localhost:7194/api/links
Content-Type: application/json

{
  "originalUrl": "https://www.microsoft.com",
  "alias": "microsoft"
}
```

Resposta de sucesso: **201 Created**, com o endereço curto no cabeçalho `Location` e no corpo:

```json
{
  "code": "microsoft",
  "shortUrl": "https://localhost:7194/s/microsoft",
  "originalUrl": "https://www.microsoft.com"
}
```

Para geração automática, omita `alias`, envie `null` ou uma string vazia. Strings contendo apenas espaços também são tratadas como ausência de alias.

```json
{
  "originalUrl": "https://www.microsoft.com",
  "alias": null
}
```

| Status | Situação |
|---|---|
| 201 | Link criado |
| 400 | Entrada inválida |
| 409 | Alias já utilizado |
| 503 | Falha após esgotar as tentativas de geração ou fila indisponível durante o encerramento |

Os erros tratados pelo controller utilizam `ProblemDetails`. Falhas inesperadas são encaminhadas ao tratamento global de exceções.

### Redirecionar

`GET /s/{code}`

- **302 Found:** redireciona para a URL original pelo cabeçalho `Location`.
- **404 Not Found:** código não encontrado.

O redirecionamento consulta diretamente o armazenamento e não passa pela fila de criação.

O arquivo `Topaz.UrlShortener/Topaz.UrlShortener.http` permite testar a API pelo Visual Studio. Ajuste a porta e o conteúdo da solicitação conforme o cenário desejado.

## Regras de validação e geração

- A URL deve ser absoluta, usar HTTP ou HTTPS, possuir host e não conter credenciais embutidas.
- Espaços nas extremidades são removidos. A URL completa não é convertida para minúsculas, preservando caminhos e parâmetros sensíveis a maiúsculas.
- O alias deve ter de 3 a 30 caracteres ASCII: letras, números, hífen ou sublinhado.
- Os códigos são consultados e reservados sem diferenciar maiúsculas de minúsculas. Assim, `microsoft` e `MICROSOFT` representam a mesma chave.
- O código automático possui oito caracteres, usando letras minúsculas e números, selecionados com `RandomNumberGenerator`.
- O gerador não garante unicidade sozinho. A reserva acontece com `TryAdd`, sem sobrescrever links existentes.
- Em caso de colisão automática, são realizadas até dez tentativas. Se todas falharem, a criação retorna indisponibilidade.
- Uma URL original pode ter vários links curtos. Não há deduplicação por destino.
- A validação verifica o formato da URL; não consulta o destino para comprovar sua disponibilidade.

## Organização do código

Um projeto web reúne a implementação, organizada por responsabilidades, e um projeto separado contém os testes.

| Pasta ou projeto | Responsabilidade |
|---|---|
| `Domain` | Representação do link encurtado |
| `Application/Interfaces` | Contratos de armazenamento e geração |
| `Application/Services` | Validação e criação de links |
| `Application/Results` | Resultados da operação, independentes de HTTP |
| `DTOs` | Contratos de entrada e saída da API |
| `Infrastructure/Storage` | Armazenamento em memória |
| `Infrastructure/Generation` | Geração aleatória de códigos |
| `Infrastructure/Queue` | Canal, itens de trabalho e consumidor |
| `Controllers` | Endpoints HTTP, redirecionamento e página MVC |
| `Views` e `wwwroot` | Interface e arquivos estáticos |
| `Topaz.UrlShortener.Tests` | Testes automatizados |

O serviço de criação não conhece status HTTP. O controller converte o resultado da aplicação em uma resposta da API. As interfaces permitem controlar as dependências nos testes sem adicionar uma biblioteca de mocks.

## Processamento sincronizado e ordem

O `LinkCreationQueue` utiliza um `Channel<LinkCreationJob>` limitado a **100 itens aguardando leitura**, com `FullMode.Wait`. Quando o canal está cheio, a escrita aguarda espaço ou cancelamento.

O `LinkCreationWorker`, implementado como `BackgroundService`, é o único consumidor. Ele chama o serviço síncrono de criação e conclui o item antes de iniciar o próximo. Não utiliza `Task.Run` ou processamento paralelo para executar as criações.

O controller aguarda o resultado do seu item antes de responder. O uso de `async/await` permite aguardar a fila sem transformar o processamento de criação em operações simultâneas.

A ordem FIFO corresponde à **entrada efetiva no canal**. Não representa o instante do clique do usuário nem uma garantia de ordem entre produtores que aguardam espaço no canal.

O `TaskCompletionSource` de cada item devolve seu resultado ao solicitante. `RunContinuationsAsynchronously` evita executar as continuações do solicitante diretamente na conclusão do item pelo consumidor.

## Decisões e trade-offs

| Decisão | Motivo e consequência |
|---|---|
| Um projeto web organizado por pastas | Mantém a solução proporcional ao desafio e reduz configuração entre projetos |
| MVC e JavaScript consumindo a API | Permite demonstrar o fluxo completo sem uma aplicação frontend separada |
| Persistência em memória | Simplifica a execução, mas os links são perdidos ao encerrar ou reciclar o processo |
| `ConcurrentDictionary` | Permite consultas concorrentes e inserção atômica enquanto o worker cria links |
| Canal com um consumidor | Atende ao processamento sequencial com ordem de entrada explícita |
| Canal limitado com espera | Limita os itens dentro do canal; não limita sozinho todas as requisições HTTP que aguardam espaço |
| Tentativas limitadas na geração | Trata colisões sem sobrescrever dados e sem repetir indefinidamente |
| Redirecionamento 302 | Evita declarar o redirecionamento como permanente |
| Organização por responsabilidades | Mantém separação clara sem introduzir múltiplos projetos de camadas ou caracterizar a solução como DDD |

## Limitações conhecidas

- A solução foi projetada para **uma instância e um processo**. Não existe coordenação distribuída entre workers de processos diferentes.
- Links e solicitações em memória são perdidos quando o processo termina, inclusive em reinicializações e reciclagens do IIS.
- Se o cliente desconectar depois de enfileirar, a criação pode continuar e reservar um código mesmo sem entregar a resposta ao cliente.
- No encerramento, novas entradas são interrompidas e itens que restarem no canal são cancelados. Não há garantia de drenagem completa da fila.
- O armazenamento não possui expiração, limite de links, autenticação ou rate limiting.
- A aplicação não implementa análise de destinos maliciosos nem proteção contra abuso de um serviço público de encurtamento.
- A execução em múltiplos processos, inclusive durante sobreposição de reciclagem do IIS, não compartilha o armazenamento nem a fila.

## Testes

Na raiz da solução:

```bash
dotnet test --configuration Release
```

Também é possível executar pelo **Gerenciador de Testes** do Visual Studio, sem iniciar a aplicação web.

Foram executados **14 casos de teste NUnit com sucesso** no ambiente de desenvolvimento, cobrindo:

- Criação e armazenamento com alias disponível.
- Rejeição de URL nula, vazia, em branco, inválida, FTP ou com credenciais.
- Conflito de alias, inclusive com diferença entre maiúsculas e minúsculas, preservando o destino original.
- Nova tentativa após colisão de código.
- Falha após dez tentativas com colisão.
- Ordem FIFO de processamento.
- Continuidade do consumidor após falha inesperada em um item.
- Exclusividade da geração em um cenário controlado com duas solicitações.

Os testes usam geradores controlados e sinais de sincronização. O `Task.Run` utilizado no teste de exclusividade permite controlar um gerador bloqueado no .NET 8; ele não faz parte do processamento do worker em produção.

Também foram realizados testes manuais da API e da interface: criação com alias, conflito 409, geração automática, entrada inválida 400, redirecionamento e cópia do link.

## Publicação no IIS

### Pré-requisitos e procedimento

1. Habilite o IIS e seu console de gerenciamento no Windows.
2. Instale o **Hosting Bundle do .NET 8** no computador de destino. Se o bundle foi instalado antes do IIS, repare ou reinstale o bundle após habilitar o IIS.
3. Publique somente o projeto web, em configuração Release e modo dependente de framework:

```powershell
dotnet publish .\Topaz.UrlShortener\Topaz.UrlShortener.csproj -c Release --self-contained false -o C:\Publicacoes\Topaz.UrlShortener
```

4. Confira se a saída contém `web.config`, a DLL da aplicação e `wwwroot`.
5. Crie um pool dedicado no IIS com **Sem Código Gerenciado** e mantenha o máximo de processos de trabalho em **1**.
6. Crie um site apontando para a pasta publicada e associe o pool dedicado.
7. Conceda à identidade do pool permissão de leitura e execução na pasta publicada.
8. Configure os bindings do site. Para HTTPS, associe um certificado confiável no ambiente de teste.
9. Acesse o endereço configurado e repita a criação, o conflito de alias e o redirecionamento.

O projeto mantém o redirecionamento para HTTPS do template. Para a apresentação, configure HTTPS no IIS e utilize o endereço desse binding. As portas do `launchSettings.json` são de desenvolvimento e não configuram os bindings do IIS.

Para diagnosticar falhas de inicialização, consulte o Visualizador de Eventos do Windows e os logs do ASP.NET Core Module, habilitando logs adicionais temporariamente quando necessário.

**Status da validação:** os testes automatizados e os fluxos manuais descritos acima foram executados em desenvolvimento. A validação da publicação no IIS local ainda está pendente.

Referência: [Hospedar ASP.NET Core no IIS — Microsoft Learn](https://learn.microsoft.com/aspnet/core/host-and-deploy/iis/?view=aspnetcore-8.0).

## Melhorias futuras

- Persistência durável e restrição de unicidade no banco de dados.
- Expiração e políticas de retenção de links.
- Rate limiting e proteção contra abuso.
- Métricas de fila, tempo de espera, falhas e colisões.
- Testes de integração HTTP e de encerramento/cancelamento.
- Automação de build e testes em pipeline de CI.
- Estratégia de coordenação e persistência compartilhada caso seja necessário executar múltiplas instâncias.

## Autor

Natanael Santos Sousa.
