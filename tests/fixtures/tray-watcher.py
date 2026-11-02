# Private-bus StatusNotifierWatcher fixture; never exports to the user's desktop.
from gi.repository import Gio, GLib
xml = '''<node><interface name="org.kde.StatusNotifierWatcher">
<method name="RegisterStatusNotifierItem"><arg type="s" direction="in"/></method>
<method name="GetItem"><arg type="s" direction="out"/></method>
<method name="SetHost"><arg type="b" direction="in"/></method>
<property name="IsStatusNotifierHostRegistered" type="b" access="read"/>
<property name="RegisteredStatusNotifierItems" type="as" access="read"/>
<property name="ProtocolVersion" type="i" access="read"/>
</interface></node>'''
node = Gio.DBusNodeInfo.new_for_xml(xml)
bus = Gio.bus_get_sync(Gio.BusType.SESSION, None)
item = ''; host = True

def method(connection, sender, path, interface, name, params, invocation):
    global item, host
    if name == 'RegisterStatusNotifierItem':
        item = sender
        invocation.return_value(GLib.Variant('()', ()))
    elif name == 'GetItem': invocation.return_value(GLib.Variant('(s)', (item,)))
    elif name == 'SetHost':
        host = params.unpack()[0]
        connection.emit_signal(None, path, 'org.freedesktop.DBus.Properties', 'PropertiesChanged', GLib.Variant('(sa{sv}as)', (interface, {'IsStatusNotifierHostRegistered': GLib.Variant('b', host)}, [])))
        invocation.return_value(GLib.Variant('()', ()))

def get_property(connection, sender, path, interface, name):
    if name == 'IsStatusNotifierHostRegistered': return GLib.Variant('b', host)
    if name == 'RegisteredStatusNotifierItems': return GLib.Variant('as', [item] if item else [])
    return GLib.Variant('i', 0)

bus.register_object('/StatusNotifierWatcher', node.interfaces[0], method, get_property, None)
Gio.bus_own_name_on_connection(bus, 'org.kde.StatusNotifierWatcher', Gio.BusNameOwnerFlags.NONE, lambda *args: print('ready', flush=True), None)
GLib.MainLoop().run()
