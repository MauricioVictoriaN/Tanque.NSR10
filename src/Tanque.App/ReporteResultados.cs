// ---------------------------------------------------------------------------
// Tanque.NSR10
// (c) Mauricio Javier Victoria Niño <hidratecsa@gmail.com> · CC BY-NC-SA 4.0
// Uso exclusivamente académico. El motor de cálculo (Tanque.Core) se distribuye
// como binario ofuscado, protegido por derechos de autor (ver LICENSE).
// ---------------------------------------------------------------------------
// Comentario de arquitectura (año de revisión, observación H3 del informe de auditoría externa del usuario): esta
// clase se movió, sin cambiar el contenido de ningún reporte producido (verificado carácter por
// carácter en tools/Tanque.Core.Verificacion, módulo 12), a la nueva biblioteca independiente
// src/Tanque.Reportes/ReporteResultados.cs (namespace Tanque.Reportes) -- ya no depende de
// Tanque.App ni de Avalonia, así que ahora es 100% verificable en el el entorno de desarrollo.
//
// Este archivo se deja vacío deliberadamente (en vez de eliminarlo) porque el bridge de esta sesión
// con el equipo del usuario no tiene una herramienta de borrado de archivos disponible -- ver
// la hoja de seguimiento del proyecto, nota de diseño v3. Puede eliminarse manualmente sin ningún
// efecto: ningún otro archivo de Tanque.App lo referencia (MainWindow.axaml.cs ahora usa
// "using Tanque.Reportes;" y llama a Tanque.Reportes.ReporteResultados.GenerarReporte).
