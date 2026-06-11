{{/* Common template helpers */}}

{{- define "canteen.name" -}}
{{- default .Chart.Name .Values.nameOverride | trunc 63 | trimSuffix "-" -}}
{{- end -}}

{{- define "canteen.fullname" -}}
{{- printf "%s-%s" .Release.Name (include "canteen.name" .) | trunc 63 | trimSuffix "-" -}}
{{- end -}}

{{- define "canteen.labels" -}}
app.kubernetes.io/name: {{ include "canteen.name" . }}
app.kubernetes.io/instance: {{ .Release.Name }}
app.kubernetes.io/managed-by: {{ .Release.Service }}
app.kubernetes.io/version: {{ default .Chart.AppVersion .Values.image.tag | quote }}
helm.sh/chart: {{ .Chart.Name }}-{{ .Chart.Version | replace "+" "_" }}
{{- end -}}

{{- define "canteen.selectorLabels" -}}
app.kubernetes.io/name: {{ include "canteen.name" . }}
app.kubernetes.io/instance: {{ .Release.Name }}
{{- end -}}

{{- define "canteen.image" -}}
{{ printf "%s:%s" .Values.image.repository (default .Chart.AppVersion .Values.image.tag) }}
{{- end -}}
