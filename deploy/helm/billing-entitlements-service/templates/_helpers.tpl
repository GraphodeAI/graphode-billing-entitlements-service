{{- define "billing-entitlements-service.name" -}}
{{- default .Chart.Name .Values.nameOverride | trunc 63 | trimSuffix "-" -}}
{{- end -}}

{{- define "billing-entitlements-service.fullname" -}}
{{- if .Values.fullnameOverride -}}
{{- .Values.fullnameOverride | trunc 63 | trimSuffix "-" -}}
{{- else -}}
{{- printf "%s-%s" .Release.Name (include "billing-entitlements-service.name" .) | trunc 63 | trimSuffix "-" -}}
{{- end -}}
{{- end -}}

{{- define "billing-entitlements-service.labels" -}}
app.kubernetes.io/name: {{ include "billing-entitlements-service.name" . }}
helm.sh/chart: {{ .Chart.Name }}-{{ .Chart.Version | replace "+" "_" }}
app.kubernetes.io/instance: {{ .Release.Name }}
app.kubernetes.io/managed-by: {{ .Release.Service }}
{{- end -}}

{{- define "billing-entitlements-service.selectorLabels" -}}
app.kubernetes.io/name: {{ include "billing-entitlements-service.name" . }}
app.kubernetes.io/instance: {{ .Release.Name }}
{{- end -}}
