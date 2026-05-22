# Contrato de respuesta de la API

> Jira: Definición e implementación de contrato de respuesta success/error

Todas las respuestas siguen una estructura uniforme, independientemente del endpoint o del tipo de error. El cuerpo siempre es JSON y siempre incluye el `statusCode` como campo raíz junto al payload principal.

---

## Respuesta exitosa

### Recurso único — 200 OK / 201 Created

```json
{
  "data": { },
  "statusCode": 200
}
```

| Campo | Tipo | Descripción |
|---|---|---|
| `data` | `object` | Payload devuelto por el caso de uso |
| `statusCode` | `int` | Código HTTP reflejado en el cuerpo |

### Colección paginada — 200 OK

```json
{
  "data": {
    "items": [],
    "totalCount": 0
  },
  "statusCode": 200
}
```

| Campo | Tipo | Descripción |
|---|---|---|
| `data.items` | `array` | Página de resultados |
| `data.totalCount` | `int` | Total de registros sin paginar |

---

## Respuesta de error

```json
{
  "error": {
    "type": "VALIDATION",
    "code": "HTTP.VALIDATION",
    "message": "Validation failed",
    "details": []
  },
  "statusCode": 400
}
```

| Campo | Tipo | Descripción |
|---|---|---|
| `error.type` | `string` | Categoría del error (ver tabla de tipos) |
| `error.code` | `string` | Código compuesto `HTTP.<TYPE>` |
| `error.message` | `string` | Descripción general del error |
| `error.details` | `array` | Lista de errores individuales |
| `statusCode` | `int` | Código HTTP reflejado en el cuerpo |

### Tipos de error

| `type` | `statusCode` | Uso |
|---|---|---|
| `VALIDATION` | 400 | Errores de formato o reglas de validación de entrada |
| `DOMAIN_VALIDATION` | 400 | Reglas de negocio del dominio |
| `NOT_FOUND` | 404 | Recurso no encontrado |
| `CONFLICT` | 409 | Conflicto de estado (ej. duplicado) |
| `UNAUTHORIZED` | 401 | Sin autenticación |
| `FORBIDDEN` | 403 | Sin permisos suficientes |
| `INTERNAL` | 500 | Error no controlado |

---

## Detalle de error (`details`)

Cada elemento del arreglo `details` describe un campo que falló:

```json
{
  "property": "email",
  "value": "no-es-un-email",
  "errors": [
    "El formato del email no es válido."
  ]
}
```

| Campo | Tipo | Obligatorio | Descripción |
|---|---|---|---|
| `property` | `string` | Sí | Nombre del campo en camelCase |
| `value` | `any` | No | Valor enviado que causó el error |
| `errors` | `string[]` | No | Mensajes de error del campo |
| `attributes` | `object` | No | Metadata adicional del error (ej. `min`, `max`) |
| `children` | `array` | No | Errores anidados para objetos complejos |

> Los campos opcionales se omiten del JSON cuando son `null`.

### Regla de `value`

`value` se incluye **únicamente en el nodo que contiene el error** (hoja), no en el nodo contenedor padre.

**Ejemplo — campo simple:**
```json
{
  "property": "temperature",
  "value": "caliente",
  "errors": ["Expected a number."]
}
```

**Ejemplo — objeto anidado:**
```json
{
  "property": "address",
  "children": [
    {
      "property": "city",
      "value": "",
      "errors": ["City is required."]
    }
  ]
}
```

### Ejemplo con `attributes`

Los validadores de dominio pueden adjuntar metadata adicional al error para que el cliente pueda construir mensajes propios:

```json
{
  "property": "temperature",
  "value": 150,
  "errors": ["Temperature is out of range."],
  "attributes": {
    "min": -60,
    "max": 55
  }
}
```

---

## Origen de los errores de validación

El contrato es uniforme independientemente de la capa que detecta el error:

| Capa | Cuándo actúa |
|---|---|
| **Formato JSON** | El cuerpo del request tiene un tipo incompatible (ej. número donde se espera string) |
| **FluentValidation** | Reglas de validación de entrada definidas en los validadores |
| **Domain error** | Reglas de negocio que devuelven `ValidationError` desde el dominio |
