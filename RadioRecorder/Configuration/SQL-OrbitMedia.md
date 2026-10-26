# Conexión SQL de RadioRecorder a OrbitMedia_Test

El proveedor SQL consulta `dbo.EMISORAS` y `dbo.PLAZAS` en `OrbitMedia_Test`.

## Credencial de acceso

Por seguridad, la contraseña no está guardada en el código. El proveedor lee la variable de entorno de Windows:

`RADIORECORDER_SQL_PASSWORD`

En PowerShell, configúrala para tu usuario con:

```powershell
[Environment]::SetEnvironmentVariable(
    "RADIORECORDER_SQL_PASSWORD",
    "PEGA_AQUI_LA_CONTRASENA_SQL",
    "User"
)
```

Cierra y vuelve a abrir Visual Studio para que el proceso reciba la variable de entorno. No guardes la contraseña en el repositorio ni la compartas en capturas o logs.

## Consulta utilizada

- `EMISORAS.NUM_EMIS` ? identificador de estación.
- `EMISORAS.SIGLAS` ? siglas de archivo.
- `EMISORAS.TIT_EMIS` ? nombre de estación.
- `EMISORAS.FRECUENCIA` ? frecuencia.
- `EMISORAS.URL` ? URL directa del stream.
- `EMISORAS.ACTIVO` ? se cargan las filas cuyo valor es 1; los NULL se consideran activos.
- `EMISORAS.NUM_PLAZA` se une con `PLAZAS.NUM_PLAZA`.
- `PLAZAS.CIUDAD` ? ciudad para la carpeta de grabaciones.
- `PLAZAS.INICIO_RADIO` y `PLAZAS.FIN_RADIO` ? horas de operación, usando solo la parte horaria.
- `PLAZAS.ZONA_HR` ? `RadioStation.RecordingFileTimeOffsetHours`; controla el desfase horario usado en el nombre del archivo, sin modificar la duración ni el horario real de grabación. Si es `NULL`, se usa `0`.

El formato del stream queda en `auto`. La consulta filtra emisoras activas (considerando los NULL como activos), con URL no nula/no vacía y `ID_MEDIO = 1`.

Si `EMISORAS.URL` contiene una página web en vez de una URL directa de audio, será necesario adaptar el mapeo/resolver; el esquema proporcionado no permite distinguirlo con certeza.
