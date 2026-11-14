# AYA: contexto del proyecto

Documento de traspaso para que otro modelo pueda continuar el trabajo sin la conversación original.
Estado a 2026-11-14. Archivo temporal: no forma parte del producto.

## 1. Qué es y para qué

- **Proyecto:** AYA (nombre provisional), para el hackathon Junction 2026 (48 h desde el 2026-11-13).
- **Reto:** Tieto, "Earned Trust in AI Code": *¿cómo puede una IA revisar código hecho por IA de forma que las personas puedan fiarse?*
- **Qué valora el reto:** el concepto pesa más que la implementación. Entregables: tarjeta de concepto, demostración en vivo, registro de lecciones y vídeo. Una persona con nombre debe seguir siendo la responsable. Hay que mostrar qué encontró la IA, qué se negó a decidir y qué no se comprobó.
- **Entrega:** repositorio público (`https://github.com/Jjumper007/ValenTech-Junction.git`) y vídeo. La demo es local. El vídeo lo hace un compañero; todo el código lo escribe el modelo.
- **Idea central:** `aya review` toma un cambio (rama o commit) y produce un informe de una página que dice qué se comprobó, con qué herramienta, qué no se comprobó y qué líneas tiene que leer todavía una persona. Ninguna afirmación del informe es la opinión de un modelo: todas salen de un compilador, una ejecución de pruebas o un diff.

## 2. Decisiones del usuario (no reabrir)

- Herramienta de línea de comandos para usuarios técnicos; aplicación web didáctica solo como opción.
- Código, interfaz y documentación del repositorio en inglés. La carpeta `explicacion/` es para el usuario y está en español.
- Solo se analiza C#. Stack: C# con Roslyn (se cambió a este stack a petición del usuario).
- Repositorio objetivo: `te-industry-tech-office/junction26-messagepack-csharp`, con cuatro ramas escritas por agentes: `ju26-issue-2283`, `ju26-issue-2234`, `ju26-issue-2129`, `ju26-issue-2297`.
- **La transformación determinista es "la esencia":** reducir al mínimo lo que depende de un modelo. El grafo se construye por dependencias reales (modelo semántico del compilador).
- Gemini será el modelo principal (es el único premio de patrocinador que interesa). **Todavía no hay clave**: de momento el escritor de pruebas usa sesiones nuevas de Claude (Opus 5.5, esfuerzo medio). Cuando haya clave, irá en la variable de entorno `GEMINI_API_KEY`, no pegada en el chat.
- El usuario aprueba cada cambio; se trabaja por partes y la primera es la validación. Los arreglos de fallos los propondrá un modelo (pendiente).
- No hacer commit ni push salvo que el usuario lo pida.
- El usuario quiere que se le dé la razón solo cuando la tiene. El objetivo que persigue es que el código sea "lo más bulletproof posible"; el encuadre honesto acordado es: las pruebas siguen siendo empíricas, y lo que AYA aporta es **mostrar exactamente dónde un fallo pasaría desapercibido**.
- Como es un prototipo, se permite instalar cosas que no se harían en producción.

## 3. Entorno

- Windows 11, PowerShell y Git Bash. Rutas largas desactivadas en Windows.
- SDK de .NET 8.0.428 y 9.0.321 instalados en `%LOCALAPPDATA%\Microsoft\dotnet` (el `dotnet` de Program Files no tiene SDK). Antes de ejecutar:
  `export DOTNET_ROOT="/c/Users/juan/AppData/Local/Microsoft/dotnet" PATH="/c/Users/juan/AppData/Local/Microsoft/dotnet:$PATH"`
- Python 3.14 y Node 24. No hay `gh` ni `pwsh`. Edge en modo headless sirve para capturas e imprimir PDF.
- Datos de trabajo en `~/.aya` (o `AYA_HOME`): clon en `~/.aya/repos/mp` (ruta corta obligatoria), árboles de trabajo en `~/.aya/wt/`.
- Los heredoc largos de Bash (unas 130 líneas o más) se truncan: para contenido grande, escribir el archivo directamente.

## 4. Cómo se ejecuta

```
dotnet build src/Aya/Aya.csproj
dotnet src/Aya/bin/Debug/net8.0/aya.dll review \
    --repo https://github.com/te-industry-tech-office/junction26-messagepack-csharp \
    --head ju26-issue-2283 --req "MessagePack-CSharp#2283" \
    --sdk latest --mutate --write-tests
```

Opciones: `--repo --head --base --req --out --sdk auto|repo|latest --skip-tests --mutate --max-mutants (20) --write-tests --max-tests (4) --writer-model (claude-opus-5-5) --writer-effort (medium)`.

Salida en `aya-out/<rama>/`: `report.md`, `evidence.json`, `graph.json`, `pipeline.log`, `runs/`, `test-requests/R#.md`, `proposed-tests/R#.cs` (o `R#.rejected.cs`).

Una revisión simple tarda de cuatro a cinco minutos y ejecuta unas 1.200 pruebas sobre las dos versiones. Con mutación y pruebas escritas, entre 5 y 12 minutos.

## 5. Qué hace el proceso, paso a paso

1. **Clasifica el diff.** Cada archivo recibe un papel (código, prueba, archivo de salida esperada, plantilla, generado, documentación, configuración) y se agrupan los archivos que recibieron exactamente la misma edición.
2. **Delimita los proyectos** afectados y sus dependientes.
3. **Ejecuta las pruebas sobre el código nuevo** midiendo qué líneas se ejecutan (Microsoft Code Coverage, formato cobertura).
4. **Construye el grafo de dependencias** con Roslyn. Nodos: tipos, métodos, constructores, propiedades, campos, eventos. Aristas: `uses`, `type-ref`, `dispatch`, `entry`. Los ciclos se colapsan (Tarjan) y cada unidad recibe una capa; la capa 0 no depende de nada.
5. **Asigna las líneas cambiadas a las unidades** que las contienen y busca todo lo que depende de ellas.
6. **Ejecuta las pruebas sobre el código antiguo** y compara prueba a prueba.
7. **Prueba de "falla antes":** ejecuta contra el código antiguo las pruebas que trajo el cambio. Una prueba que pasa sin el cambio no demuestra nada sobre él.
8. **Atribuye la evidencia** a cada unidad, de abajo arriba.
9. **Mutación (`--mutate`):** altera cada línea cambiada, una cada vez, recompila y vuelve a ejecutar solo los proyectos de pruebas que ejecutan esa línea. "Detectada" significa que una prueba que pasaba ahora falla.
10. **Pruebas escritas (`--write-tests`):** pide a un modelo una prueba por cada tramo no ejecutado y por cada rotura no detectada. Solo se acepta si compila, pasa y se mide que ejecuta las líneas pedidas (o que falla con la línea alterada). Si no, se le devuelve el error, hasta tres intentos. Las aceptadas son propuestas: no se añaden al cambio.
11. **Escribe el informe:** plan de lectura, qué cambió, unidades cambiadas, qué se comprobó, ejecuciones, tabla de roturas, tabla de pruebas escritas, qué no se comprobó, qué se negó a decidir, límites y bloque de firma. El informe cita el hash de la evidencia.

Detalles técnicos relevantes:

- **SDK:** `dotnet` elige el SDK por el `global.json` del directorio actual. El generador del repositorio objetivo necesita un compilador más nuevo que el SDK que fija (error CS9057), así que con .NET 8 tres proyectos de pruebas no compilan. `--sdk auto` lo detecta y cambia al SDK más nuevo para las dos versiones.
- **Alteraciones del mutador:** intercambio de operadores relacionales y lógicos, `is X` por `is not X`, `!x` por `x`, `true` por `false`, negar la condición de un `if` o ternario, quitar una instrucción de expresión, quitar `continue` o `break`, quedarse con una sola mitad de un `&&` o `||`, sustituir por `default` un valor devuelto o una llamada pasada como argumento, sumar 1 a un número entero, intercambiar `+` con `-` y `*` con `/`. No altera textos ni asignaciones. El tope de roturas se reparte primero entre unidades y después entre líneas.
- **Comprobación cruzada:** cada prueba aceptada se ejecuta también contra las demás roturas no detectadas; si las hace fallar, esa petición queda como `caught-by-other`.
- **Escritor de pruebas:** `claude -p --model claude-opus-5-5 --effort medium --tools "" --strict-mcp-config --no-session-persistence --output-format text`, con el prompt por la entrada estándar y directorio `~/.aya/model-cwd`. La clase debe llamarse `Aya{R#}Tests` y se coloca temporalmente en `<proyecto de pruebas>/AyaCandidates/`.
- Las constantes no cuentan como "líneas no ejecutadas" (`Node.IsConstant`).

## 6. Archivos

| Archivo | Contenido |
|---|---|
| `src/Aya/Aya.csproj` | Aplicación de consola net8.0, ensamblado `aya`, paquetes Roslyn 4.11.0 |
| `src/Aya/Program.cs` | Ayuda y comando `review` |
| `src/Aya/Infra.cs` | Procesos, git, localizador de `dotnet`, traza (`pipeline.log`), opciones, rutas |
| `src/Aya/CodeGraph.cs` | Nodos, aristas, alcance, capas y construcción del grafo con Roslyn |
| `src/Aya/Diff.cs` | Lectura y clasificación del diff, grupos de ediciones idénticas, mapa de proyectos |
| `src/Aya/TestRunner.cs` | Ejecución de pruebas, lectura de resultados y de cobertura |
| `src/Aya/Review.cs` | Orquestación, modelos de datos, comparación, evidencias, hallazgos, plan de lectura |
| `src/Aya/Report.cs` | Generación de `report.md` |
| `src/Aya/Mutation.cs` | Banco de trabajo, mutador y veredicto de cada rotura |
| `src/Aya/TestWriter.cs` | Peticiones, prompt, llamada al modelo y comprobación de las pruebas escritas |
| `README.md` | Descripción, resultados, instrucciones y límites (inglés) |
| `TODO.md` | Aplazado: optimizaciones de rendimiento, modo automático, demo sobre el propio código |
| `explicacion/` | Ignorada por git, en español: `build.py`, plantillas, `grafos.html`, `arbol.html`, `arquitectura.pdf`, `logs/` |
| `aya-out/` | Ignorada por git: resultados de las cuatro ramas |

Visores (se regeneran con `python explicacion/build.py`):

- **`grafos.html`:** cajas anidadas desplegables, con pestañas de grafo, registro paso a paso y evidencias. No muestra resultados de pruebas ni roturas.
- **`arbol.html`:** árbol completo desplegado de arriba abajo, arrastrable, con buscador a la derecha. Las pruebas son rombos coloreados por resultado. Toda unidad de código cambiada lleva anillo: verde (todas las roturas probadas se detectaron), rojo (alguna no), gris discontinuo (ninguna probada).

## 7. Resultados actuales

Ejecuciones con `--sdk latest --mutate --write-tests`.

| Rama | Archivos | Líneas cambiadas | Líneas a leer | Roturas | Pruebas escritas |
|---|---:|---:|---:|---|---|
| `ju26-issue-2283` | 4 | 228 | 14 | 8 de 11 detectadas | 2 de 3 aceptadas |
| `ju26-issue-2234` | 4 | 266 | 108 | 7 de 7 detectadas | 1 de 1 aceptada |
| `ju26-issue-2297` | 237 | 1.392 | 31 | 7 de 7 detectadas | no hacían falta |
| `ju26-issue-2129` | 28 | 1.043 | 349 | 12 de 16 detectadas | 4 de 4 aceptadas |

Hallazgos por rama:

- **2283 (única rama ejecutada con el mutador ampliado y sin tope; las otras tres son de la versión anterior con tope 20):** las 3 pruebas nuevas fallan sobre el código antiguo y las 10 líneas cambiadas se ejecutan. Se probaron 13 roturas sobre las 6 líneas de código cambiadas, repartidas en las 4 unidades: 2 no compilan, 8 se detectan y 3 no, las tres sobre la condición de `TypeCollector.cs:1446` (`ReportIfLocatedInCompilation`). Pruebas escritas: R2 aceptada (un tipo defectuoso definido en el código fuente de otra compilación), que además detecta `&&` por `||`; R3 aceptada pero artificial: la rotura "quedarse solo con la segunda mitad" no cambia nada observable, y el modelo fabricó con reflexión un estado imposible para pasar la comprobación. Lección: "aceptada" no significa "digna de adoptarse".
- **2234:** 6 de las 10 pruebas nuevas pasan también sobre el código antiguo. 5 líneas cambiadas no se ejecutaban (1148 y 1179 a 1182); la prueba escrita las ejecuta.
- **2297:** 221 archivos de salida esperada comparten una misma edición de 3 líneas. Sin huecos.
- **2129:** 65 líneas ejecutables no se ejecutan. 4 roturas no detectadas en `GeneratedFormatterCollector.cs` (líneas 55, 66, 76 y 84). De 109 roturas posibles se probaron 20; de 14 huecos se pidieron 4 pruebas.

El modelo ha escrito en total 12 pruebas aceptadas (5 archivos) y 1 rechazada, en 8 sesiones.

Cobertura real de la mutación por unidades de código cambiadas:

| Rama | Unidades cambiadas | Con roturas probadas | Sin ninguna |
|---|---:|---:|---:|
| 2283 | 4 | 4 | 0 |
| 2234 | 1 | 1 | 0 |
| 2297 | 7 | 6 | 1 |
| 2129 | 30 | 2 | 28 |

En la 2129 las 16 roturas juzgadas cayeron en dos métodos. Motivos de las 28 sin probar: el tope se gasta en orden de archivo en vez de repartirse (11 unidades), líneas que ninguna prueba ejecuta (2), código que el mutador no sabe alterar (7) y declaraciones sin código ejecutable (8).

## 8. Límites conocidos

- AYA no juzga si el cambio cumple su requisito, y el informe lo dice.
- Una línea ejecutada por una prueba que pasa no está demostrada como correcta.
- El vínculo entre prueba y unidad es alcance estático: sobreestima.
- Las llamadas por reflexión, código generado o analizadores cargados por el compilador no son aristas del grafo.
- Los símbolos borrados no tienen nodo.
- Un método nuevo puede aparecer como "modificado" si el diff deja su llave de cierre como contexto.
- Las pruebas aceptadas comprueban lo que el modelo eligió afirmar; AYA solo garantiza que compilan, pasan y llegan a su objetivo.
- Los visores se han verificado con capturas automáticas; arrastrar y la rueda no se han probado a mano.
- `arquitectura.pdf` tiene las cifras al día pero no explica la mutación ni las pruebas escritas.

## 9. Pendiente

Ofrecido al usuario y sin respuesta todavía:

- Repetir las ramas 2234, 2297 y 2129 con el mutador ampliado y sin tope (la 2129 serán bastantes más de 109 roturas, a unos 12 segundos cada una).
- Detectar las roturas que no cambian nada observable, para no pedir pruebas imposibles.
- Reanudar una revisión desde un paso intermedio en vez de repetirla entera.

Sin empezar:

- Cambiar el escritor de pruebas a Gemini cuando haya clave.
- Lectura del requisito con Gemini (criterios de aceptación enlazados con la evidencia).
- Arreglos propuestos por un modelo.
- Actualizar `arquitectura.pdf` con mutación y pruebas escritas.
- Tarjeta de concepto y registro de lecciones para Tieto.
- Lo aplazado en `TODO.md`.
