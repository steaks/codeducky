Recent I've been working on a side project <a href="http://www.battlestates.org">Battle States</a>.  It's a simple game modeled off of Mr. Scocus' (my 6th grade Social Studies teacher) competition that helped us learn state/country names and capitals.  I can name all 50 state capitals thanks to him!  The backened is powered by django, but most of the logic is written in Javascript.  I noticed while developing and in production that often my browser was too aggresively using cached asset files rather than pulling updated assets from my server.  This problem was particularly concerning because so much of the logic is written in Javascript.

<h3>Problem</h3>
So I had a problem, I wanted to have my users' browsers appropriately use cached assets the cached versions were not out of date and to pull updated assets when the cached assets were out of date.  This being a side project, I looked for a quick/simple solution.  I found one that is simple and I think worth sharing.

<h3>Solution</h3>
The solution I decided to go with was to include the timestamp of the asset's last modification in the request uri.  The standard way to create a uri that requests the Game.js asset in django is to use the static template tag.
<pre>
<script type="text/javascript" src="{% static "game/js/Game.js" %}"></script>
</pre>
renders
<pre>
<script type="text/javascript" src="/static/game/js/Game.js"></script>
</pre>
<br/>
I created (inspired by responses to <a href="http://stackoverflow.com/questions/17777429/django-static-files-are-not-updated">Django static files are not updated</a> and <a href="https://bitbucket.org/ad3w/django-sstatic/src/4401a4bc3058618dfc2eafaee6a23d287a99ede5/sstatic/templatetags/sstatic.py?at=default">Svyatoslav Bulbakha's sstatic template tag</a>) a sstatic template tag that includes the timestamp of the asset's last modification in the uri.
<pre>
<script type="text/javascript" src="{% sstatic "game/js/Game.js" %}"></script>
</pre>
renders
<pre>
<script type="text/javascript" src="/static/game/js/Game.js?1400342030.89"></script>
</pre>
<pre>
import os
from django import template
from django.conf import settings
register = template.Library()

@register.simple_tag
def sstatic(path):
    '''
    Returns absolute URI to static file with versioning.
    '''
    static_file_dirs = [settings.STATIC_ROOT] + settings.STATICFILES_DIRS
    for full_path in map(lambda d: os.path.join(d, path), static_file_dirs):
        if os.path.isfile(full_path):
            # Get file modification time.
            mtime = os.path.getmtime(full_path)
            return '%s%s?%s' % (settings.STATIC_URL, path, mtime)
    #fallback to static template helper functionality if the file wasn't found
    return '%s%s' % (settings.STATIC_URL, path)
</pre>

Including a timestamp in the uri forces the browser to request a new asset every time the asset is modified because the request uri for the asset changes everytime it is updated.  Now the browser correctly uses up to date cached assets and requests newly modified assets.  I'm not sure how appliciable this solution is for all environments, but it works brilliantly for my simple, one server, low traffic application.
