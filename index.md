---
title: Online Hosted Instructions
permalink: index.html
layout: home
---

This page lists exercises for [AB-400: Extend Microsoft Power Platform solutions with code and AI](https://learn.microsoft.com/training/courses/ab-400t00).

<hr>

{% assign labs = site.pages | where_exp:"page", "page.url contains '/Instructions/Labs'" | where_exp:"page", "page.lab.title" | sort: "url" %}
{% for activity in labs %}
{% if activity.lab.title %}
### [{{ activity.lab.title }}]({{ site.github.url }}{{ activity.url }})
{% if activity.lab.level %}**Level**: {{activity.lab.level}} \| {% endif %}{% if activity.lab.duration %}**Duration**: {{activity.lab.duration}} minutes{% endif %}

{% if activity.lab.description %}
*{{activity.lab.description}}*
{% endif %}
<hr>
{% endif %}
{% endfor %}
